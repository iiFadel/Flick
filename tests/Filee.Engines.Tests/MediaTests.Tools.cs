// fork: the Tools wheel's video and audio operations with the real FFmpeg (skipped where it is not installed).

using Filee.Core.Conversion;
using Filee.Engines.Infrastructure;

namespace Filee.Engines.Tests;

public partial class MediaTests
{
    private async Task<string> RunToolAsync(string tool, string source)
    {
        var before = ToolOperationTests.Hash(source);
        var job = await fx.ConvertAsync([source], ToolOperationTests.Operation(tool, Path.GetExtension(source).TrimStart('.')));
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.Equal(before, ToolOperationTests.Hash(source));
        return Assert.Single(job.Outputs);
    }

    private static async Task<string> TagsAsync(string path)
    {
        var result = await ProcessRunner.RunAsync(Tools!.Value.Ffprobe,
            ["-v", "error", "-show_entries", "format_tags:stream_tags", "-of", "default=noprint_wrappers=1", path],
            TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return result.StandardOutput;
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("mkv")]
    public async Task Mute_drops_the_sound_and_copies_the_picture(string format)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip." + format, extra: ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac"]);

        var output = await RunToolAsync("muteVideo", source);

        Assert.Equal(Path.Combine(folder, $"clip Muted.{format}"), output);
        var probed = await ProbeAsync(output);
        Assert.Null(probed.Audio);
        Assert.Equal("h264", probed.Video?.Codec);
        Assert.Equal((await ProbeAsync(source)).Video?.Frames, probed.Video?.Frames);
    }

    [Theory]
    [InlineData("mp3", "mp3")]
    [InlineData("m4a", "aac")]
    [InlineData("flac", "flac")]
    public async Task Metadata_strips_the_tags_of_a_song_without_encoding_it_again(string format, string codec)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var tone = await MakeToneAsync(folder, "tone.wav");
        var source = Path.Combine(folder, "song." + format);
        await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-i", tone,
            "-metadata", "title=Secret title", "-metadata", "artist=Someone", source]);
        Assert.Contains("Secret title", await TagsAsync(source));

        var output = await RunToolAsync("removeMetadata", source);

        Assert.Equal(Path.Combine(folder, $"song No Metadata.{format}"), output);
        var tags = await TagsAsync(output);
        Assert.DoesNotContain("Secret title", tags);
        Assert.DoesNotContain("Someone", tags);
        Assert.Equal(codec, (await ProbeAsync(output)).Audio?.Codec);
    }

    [Fact]
    public async Task Metadata_strips_the_tags_of_a_video()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mp4",
            extra: ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-metadata", "title=Secret title", "-metadata", "location=+48.85+002.35/"]);

        var output = await RunToolAsync("removeMetadata", source);

        var tags = await TagsAsync(output);
        Assert.DoesNotContain("Secret title", tags);
        Assert.DoesNotContain("+48.85", tags);
        var probed = await ProbeAsync(output);
        Assert.Equal("h264", probed.Video?.Codec);
        Assert.NotNull(probed.Audio);
    }
}
