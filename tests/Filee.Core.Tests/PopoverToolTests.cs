// fork: Speed, Split, Snapshot, Visualize and Add BG: which files they run on, the popover's choices (decided
// 22 Oct 2026) and what a pick sets.

using Filee.Core.Presets;
using Filee.Core.Wheels;

namespace Filee.Core.Tests;

public class PopoverToolTests
{
    private static string Text(string key) => key switch
    {
        "tool.choice.parts" => "{0} parts",
        "tool.choice.frames" => "{0} frames",
        "tool.choice.middle_frame" => "Middle",
        "tool.suffix.changeVideoSpeed" => "Speed",
        "tool.suffix.splitVideo" => "Clips",
        "tool.suffix.videoSnapshots" => "Snapshot",
        "tool.suffix.videoSnapshots_several" => "Snapshots",
        "tool.suffix.audioToVideo" => "Visualizer",
        "tool.suffix.frameImage" => "Background",
        _ => key,
    };

    private static Preset Operation(string tool, string format) =>
        ToolOperations.For(tool, [format], id => Text("tool.suffix." + id)) ?? throw new InvalidOperationException(tool);

    private static ToolParameter Parameter(string tool) => ToolOperations.ParameterFor(tool, Text)!;

    private static Preset Pick(string tool, string format, int choice) => Parameter(tool).Apply(Operation(tool, format), choice);

    [Theory]
    [InlineData("changeVideoSpeed", "mp4")]
    [InlineData("splitVideo", "mov")]
    [InlineData("videoSnapshots", "mkv")]
    [InlineData("audioToVideo", "mp3")]
    [InlineData("audioToVideo", "wav")]
    [InlineData("frameImage", "jpg")]
    [InlineData("frameImage", "heic")]
    public void Each_runs_on_its_category(string tool, string format) => Assert.NotNull(Operation(tool, format));

    [Theory]
    [InlineData("changeVideoSpeed", "mp3")]
    [InlineData("splitVideo", "pdf")]
    [InlineData("videoSnapshots", "gif")]
    [InlineData("audioToVideo", "mp4")]
    [InlineData("frameImage", "gif")]
    [InlineData("frameImage", "mp4")]
    public void And_nowhere_else(string tool, string format) => Assert.Null(ToolOperations.For(tool, [format], _ => "x"));

    [Fact]
    public void Speed_offers_five_speeds_two_times_first()
    {
        var speed = Parameter("changeVideoSpeed");
        Assert.Equal(["0.25×", "0.5×", "1.5×", "2×", "4×"], speed.Choices.Select(c => c.Label));
        Assert.Equal("2×", speed.Choices[speed.Default].Label);

        var quarter = Pick("changeVideoSpeed", "mp4", 0);
        Assert.Equal(0.25, quarter.Media.Speed);
        Assert.Equal("{name} Speed 0.25x", quarter.Output.FileNamePattern);
        Assert.Equal(BuiltInData.SameAsSource, quarter.TargetFormat);
        Assert.Equal(4, Pick("changeVideoSpeed", "mp4", 4).Media.Speed);
    }

    [Fact]
    public void Split_cuts_into_two_three_or_four_clips_in_a_folder()
    {
        Assert.Equal(["2 parts", "3 parts", "4 parts"], Parameter("splitVideo").Choices.Select(c => c.Label));
        var three = Pick("splitVideo", "mp4", 1);
        Assert.Equal(3, three.Media.SplitParts);
        Assert.Equal("{name} Clips", three.Output.FileNamePattern);
        Assert.Equal(BuiltInData.SameAsSource, three.TargetFormat);
    }

    [Fact]
    public void Snapshot_is_one_png_or_a_folder_of_them()
    {
        Assert.Equal(["Middle", "5 frames", "10 frames"], Parameter("videoSnapshots").Choices.Select(c => c.Label));
        var one = Pick("videoSnapshots", "mp4", 0);
        Assert.Equal(("png", 1, "{name} Snapshot"), (one.TargetFormat, one.Media.Snapshots, one.Output.FileNamePattern));
        var ten = Pick("videoSnapshots", "mp4", 2);
        Assert.Equal((10, "{name} Snapshots"), (ten.Media.Snapshots, ten.Output.FileNamePattern));
    }

    [Fact]
    public void Visualize_picks_the_shape_of_the_video()
    {
        Assert.Equal(["tool.choice.landscape", "tool.choice.portrait", "tool.choice.square"],
            Parameter("audioToVideo").Choices.Select(c => c.Label));
        Assert.Equal(VisualizerShape.Landscape, Operation("audioToVideo", "flac").Media.Visualizer);
        var square = Pick("audioToVideo", "m4a", 2);
        Assert.Equal(VisualizerShape.Square, square.Media.Visualizer);
        Assert.Equal("{name} Visualizer", square.Output.FileNamePattern);
    }

    [Fact]
    public void Add_bg_puts_the_picture_on_white_black_or_orange()
    {
        Assert.Equal(3, Parameter("frameImage").Choices.Count);
        Assert.Equal(ImageBackdrop.White, Operation("frameImage", "png").Image.Backdrop);
        var orange = Pick("frameImage", "png", 2);
        Assert.Equal(ImageBackdrop.OrangeGradient, orange.Image.Backdrop);
        Assert.Equal("{name} Background", orange.Output.FileNamePattern);
        Assert.True(orange.Image.KeepMetadata);
    }
}
