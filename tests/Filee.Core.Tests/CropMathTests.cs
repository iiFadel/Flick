// fork: the crop editors' geometry (Core/Editing/CropMath.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class CropMathTests
{
    [Fact]
    public void Clamp_keeps_a_crop_inside_the_picture()
    {
        Assert.Equal(new NormalizedRect(0, 0.5, 1, 0.5), CropMath.Clamp(new NormalizedRect(-0.2, 0.5, 2, 0.9)));
        Assert.Equal(new NormalizedRect(0, 0, 0, 0), CropMath.Clamp(new NormalizedRect(double.NaN, double.PositiveInfinity, -1, double.NaN)));
    }

    [Fact]
    public void ToPixels_rounds_and_never_gives_an_empty_crop()
    {
        Assert.Equal(new PixelRect(80, 100, 160, 100), CropMath.ToPixels(new NormalizedRect(0.25, 0.5, 0.5, 0.5), 320, 200));
        Assert.Equal(new PixelRect(319, 199, 1, 1), CropMath.ToPixels(new NormalizedRect(1, 1, 0, 0), 320, 200));
    }

    [Fact]
    public void ToPixels_even_rounds_down_to_even_numbers_for_video()
    {
        var pixels = CropMath.ToPixels(new NormalizedRect(0.1, 0.1, 0.5, 0.5), 333, 211, even: true);

        Assert.All(new[] { pixels.X, pixels.Y, pixels.Width, pixels.Height }, value => Assert.Equal(0, value % 2));
        Assert.InRange(pixels.X + pixels.Width, 0, 333);
        Assert.InRange(pixels.Y + pixels.Height, 0, 211);
        Assert.Equal(new PixelRect(318, 198, 2, 2), CropMath.ToPixels(new NormalizedRect(1, 1, 0, 0), 320, 200, even: true));
    }

    [Fact]
    public void Exact_centres_the_size_typed_on_the_box_and_keeps_it_inside()
    {
        var centred = CropMath.Exact(100, 50, 400, 200, new NormalizedRect(0.25, 0.25, 0.5, 0.5));
        Assert.Equal(new PixelRect(150, 75, 100, 50), CropMath.ToPixels(centred, 400, 200));

        var atEdge = CropMath.Exact(100, 50, 400, 200, new NormalizedRect(0.9, 0.9, 0.1, 0.1));
        Assert.Equal(new PixelRect(300, 150, 100, 50), CropMath.ToPixels(atEdge, 400, 200));

        var tooLarge = CropMath.Exact(1000, 1000, 400, 200, new NormalizedRect(0, 0, 0.5, 0.5));
        Assert.Equal(NormalizedRect.Full, tooLarge);
    }

    [Theory]
    [InlineData("free", 0)]
    [InlineData("1:1", 1)]
    [InlineData("16:9", 16.0 / 9)]
    [InlineData("9:16", 9.0 / 16)]
    [InlineData("original", 2)]
    [InlineData("nonsense", 0)]
    public void AspectRatio_is_width_over_height(string choice, double expected) =>
        Assert.Equal(expected, CropMath.AspectRatio(choice, 400, 200), 6);
}
