// fork: the Tools wheel's popover operations end to end: Add BG here, Speed, Split, Snapshot and Visualize in
// MediaTests.PopoverTools.cs (they need FFmpeg). Inputs are made here; the source must come out untouched.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Core.Wheels;
using ImageMagick;

namespace Filee.Engines.Tests;

public class PopoverToolTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static string Text(string key) => key switch
    {
        "tool.choice.parts" => "{0} parts",
        "tool.choice.frames" => "{0} frames",
        "tool.suffix.changeVideoSpeed" => "Speed",
        "tool.suffix.splitVideo" => "Clips",
        "tool.suffix.videoSnapshots" => "Snapshot",
        "tool.suffix.videoSnapshots_several" => "Snapshots",
        "tool.suffix.audioToVideo" => "Visualizer",
        "tool.suffix.frameImage" => "Background",
        _ => key,
    };

    /// <summary>The preset <paramref name="tool"/> runs on a <paramref name="format"/> file after choice <paramref name="choice"/>.</summary>
    internal static Preset Pick(string tool, string format, int choice)
    {
        var operation = ToolOperations.For(tool, [format], id => Text("tool.suffix." + id))
            ?? throw new InvalidOperationException($"No {tool} for {format}.");
        return ToolOperations.ParameterFor(tool, Text)!.Apply(operation, choice);
    }

    [Theory]
    [InlineData("jpg", 0, "#FFFFFF")]
    [InlineData("png", 1, "#000000")]
    [InlineData("webp", 2, "#FF6A1E")]
    public async Task Add_bg_pads_the_picture_and_rounds_its_corners(string format, int choice, string background)
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo." + format);
        using (var photo = new MagickImage(MagickColors.SteelBlue, 400, 200))
            photo.Write(source);
        var before = ToolOperationTests.Hash(source);

        var job = await fx.ConvertAsync([source], Pick("frameImage", format, choice));

        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, $"photo Background.{format}"), output);
        Assert.Equal(before, ToolOperationTests.Hash(source));
        using var result = new MagickImage(output);
        // 8 % of the longer side (32 px) on every side.
        Assert.Equal((464u, 264u), (result.Width, result.Height));
        using var pixels = result.GetPixels();
        bool Near(int x, int y, IMagickColor<byte> expected, int tolerance)
        {
            var c = pixels.GetPixel(x, y).ToColor()!;
            return Math.Abs(c.R - expected.R) <= tolerance && Math.Abs(c.G - expected.G) <= tolerance && Math.Abs(c.B - expected.B) <= tolerance;
        }
        // The gradient runs from #FF8A3D to #FF5200 top to bottom: about its middle colour half way down.
        Assert.True(Near(5, 132, new MagickColor(background), 16), "background");
        // The picture's own corner is rounded off: the background shows there, the picture just inside it.
        Assert.False(Near(32, 32, MagickColors.SteelBlue, 40), "rounded corner");
        Assert.True(Near(232, 132, MagickColors.SteelBlue, 8), "picture");
    }

    [Fact]
    public void No_backdrop_leaves_the_picture_as_it_is()
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 40, 20);
        Filee.Engines.Magick.ImageBackdrops.Apply(image, ImageBackdrop.None);
        Assert.Equal((40u, 20u), (image.Width, image.Height));
    }
}
