// fork: the value popover's choices: the keys wrap around, a pick changes a copy of the operation's preset only.

using Filee.Core.Presets;
using Filee.Core.Wheels;

namespace Filee.Core.Tests;

public class ToolParameterTests
{
    private static ToolParameter Channels() => ToolOperations.ParameterFor("audioChannels", key => key switch
    {
        "tool.choice.mono" => "Mono",
        "tool.choice.stereo" => "Stereo",
        _ => key,
    })!;

    [Fact]
    public void Channels_asks_mono_or_stereo_mono_first()
    {
        var channels = Channels();
        Assert.Equal(["Mono", "Stereo"], channels.Choices.Select(c => c.Label));
        Assert.Equal(0, channels.Default);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(0, -1, 1)]
    [InlineData(1, -3, 0)]
    public void The_keys_wrap_around(int from, int step, int expected) => Assert.Equal(expected, Channels().Move(from, step));

    [Fact]
    public void A_pick_changes_a_copy_named_after_it()
    {
        var operation = ToolOperations.For("audioChannels", ["mp3"], _ => "Channels")!;
        operation.Name = "Channels";

        var stereo = Channels().Apply(operation, 1);

        Assert.Equal(2, stereo.Media.AudioChannels);
        Assert.Equal("Channels Stereo", stereo.Name);
        Assert.Equal("{name} Stereo", stereo.Output.FileNamePattern);
        Assert.Equal(BuiltInData.SameAsSource, stereo.TargetFormat);
        Assert.Equal(0, operation.Media.AudioChannels); // the wheel's preset stays as it was
        Assert.Equal(1, Channels().Apply(operation, 0).Media.AudioChannels);
    }

    [Theory]
    [InlineData("compress")]
    [InlineData("removeMetadata")]
    [InlineData("muteVideo")]
    public void Instant_tools_ask_nothing(string tool) => Assert.Null(ToolOperations.ParameterFor(tool, key => key));

    [Fact]
    public void Channels_runs_on_songs_only()
    {
        Assert.NotNull(ToolOperations.For("audioChannels", ["wav", "flac"], _ => "Channels"));
        Assert.Null(ToolOperations.For("audioChannels", ["mp4"], _ => "Channels"));
    }
}
