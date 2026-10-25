// fork: the Edit editor's sliders (Core/Editing/Adjustments.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class AdjustmentsTests
{
    [Fact]
    public void Every_slider_at_zero_changes_nothing()
    {
        Assert.True(Adjustments.None.IsNone);
        Assert.True(new Adjustments().IsNone);
        Assert.False(new Adjustments { Warmth = 1 }.IsNone);
    }

    [Fact]
    public void Sliders_stay_in_their_range()
    {
        var clamped = new Adjustments { Exposure = 500, Contrast = -500, Sharpness = -20, Saturation = 40 }.Clamped();

        Assert.Equal(new Adjustments { Exposure = 100, Contrast = -100, Sharpness = 0, Saturation = 40 }, clamped);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 2)]
    [InlineData(-100, 0.25)]
    [InlineData(300, 4)]
    public void Exposure_is_a_stop_per_fifty(int exposure, double factor) =>
        Assert.Equal(factor, new Adjustments { Exposure = exposure }.ExposureFactor, 9);
}
