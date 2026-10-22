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

    [Fact]
    public async Task Compress_makes_a_wav_a_flac_and_an_mp3_128k()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var wav = await MakeToneAsync(folder, "voice.wav", seconds: 4);

        var flac = await RunToolAsync("compress", wav);
        Assert.Equal(Path.Combine(folder, "voice Compressed.flac"), flac);
        Assert.Equal("flac", (await ProbeAsync(flac)).Audio?.Codec);

        var mp3 = Path.Combine(folder, "song.mp3");
        await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-i", wav, "-b:a", "320k", mp3]);
        var smaller = await RunToolAsync("compress", mp3);
        Assert.Equal(Path.Combine(folder, "song Compressed.mp3"), smaller);
        Assert.InRange((await ProbeAsync(smaller)).Audio!.BitRate, 100_000, 140_000);
    }

    [Fact]
    public async Task Compress_makes_a_video_smaller_in_its_own_format()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mp4", seconds: 3, size: "640x480",
            extra: ["-c:v", "libx264", "-crf", "12", "-pix_fmt", "yuv420p", "-c:a", "aac"]);

        var output = await RunToolAsync("compress", source);

        Assert.Equal(Path.Combine(folder, "clip Compressed.mp4"), output);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length);
        Assert.Equal("h264", (await ProbeAsync(output)).Video?.Codec);
    }

    [Fact]
    public async Task Normalize_brings_a_quiet_song_to_minus_16_lufs()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var tone = await MakeToneAsync(folder, "tone.wav", seconds: 6);
        var quiet = Path.Combine(folder, "quiet.flac");
        await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-i", tone, "-af", "volume=-20dB", quiet]);

        var output = await RunToolAsync("normalizeAudio", quiet);

        Assert.Equal(Path.Combine(folder, "quiet Normalized.flac"), output);
        Assert.InRange(await LoudnessAsync(output), -17.5, -14.5);
        Assert.Equal(44100, (await ProbeAsync(output)).Audio?.SampleRate);
    }

    /// <summary>Integrated loudness (LUFS) as FFmpeg's EBU R128 meter measures it.</summary>
    private static async Task<double> LoudnessAsync(string path)
    {
        var result = await ProcessRunner.RunAsync(Tools!.Value.Ffmpeg,
            ["-hide_banner", "-nostdin", "-i", path, "-af", "ebur128", "-f", "null", "-"], TimeSpan.FromMinutes(1), CancellationToken.None);
        var line = result.StandardError.Split('\n').Last(l => l.TrimStart().StartsWith("I:", StringComparison.Ordinal));
        return double.Parse(line.Split(':')[1].Replace("LUFS", "").Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
