// fork: the Tools wheel's Speed, Split, Snapshot and Visualize with the real FFmpeg (skipped where it is not installed).

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Media;
using ImageMagick;

namespace Filee.Engines.Tests;

public partial class MediaTests
{
    private async Task<string> RunPickAsync(string tool, int choice, string source)
    {
        var before = ToolOperationTests.Hash(source);
        var job = await fx.ConvertAsync([source], PopoverToolTests.Pick(tool, Path.GetExtension(source).TrimStart('.'), choice));
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.Equal(before, ToolOperationTests.Hash(source));
        return Assert.Single(job.Outputs);
    }

    [Theory]
    [InlineData(0, "0.25", 16)]
    [InlineData(3, "2", 2)]
    [InlineData(4, "4", 1)]
    public async Task Speed_plays_the_video_and_its_sound_faster_or_slower(int choice, string label, double seconds)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mp4", seconds: 4, extra: ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac"]);

        var output = await RunPickAsync("changeVideoSpeed", choice, source);

        Assert.Equal(Path.Combine(folder, $"clip Speed {label}x.mp4"), output);
        var probed = await ProbeAsync(output);
        Assert.InRange(probed.Duration, seconds * 0.9, seconds * 1.1 + 0.1);
        Assert.Equal("h264", probed.Video?.Codec);
        Assert.Equal(44100, probed.Audio?.SampleRate);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 4)]
    public async Task Split_cuts_the_video_into_equal_clips_in_a_folder(int choice, int parts)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        // A keyframe every half second, so every cut has one near it.
        var source = await MakeClipAsync(folder, "clip.mov", seconds: 8, extra: ["-c:v", "libx264", "-g", "12", "-pix_fmt", "yuv420p", "-c:a", "aac"]);

        var output = await RunPickAsync("splitVideo", choice, source);

        Assert.Equal(Path.Combine(folder, "clip Clips"), output);
        var clips = Directory.GetFiles(output).Order().ToList();
        Assert.Equal(Enumerable.Range(1, parts).Select(i => Path.Combine(output, $"clip {i}.mov")), clips);
        var total = 0.0;
        foreach (var clip in clips)
        {
            var probed = await ProbeAsync(clip);
            Assert.InRange(probed.Duration, 8.0 / parts - 0.7, 8.0 / parts + 0.7);
            Assert.Equal("h264", probed.Video?.Codec);
            Assert.NotNull(probed.Audio);
            total += probed.Duration;
        }
        Assert.InRange(total, 7.5, 8.5);
        Assert.Empty(Directory.GetDirectories(folder).Where(d => Path.GetFileName(d).StartsWith('.')));
    }

    [Fact]
    public async Task Snapshot_takes_the_middle_frame_as_a_full_size_png()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mkv", seconds: 4, size: "640x360");

        var output = await RunPickAsync("videoSnapshots", 0, source);

        Assert.Equal(Path.Combine(folder, "clip Snapshot.png"), output);
        using var picture = new MagickImage(output);
        Assert.Equal((MagickFormat.Png, 640u, 360u), (picture.Format, picture.Width, picture.Height));
    }

    [Fact]
    public async Task Snapshot_of_five_frames_fills_a_folder_with_different_pictures()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mp4", seconds: 5, extra: ["-c:v", "libx264", "-pix_fmt", "yuv420p"]);

        var output = await RunPickAsync("videoSnapshots", 1, source);

        Assert.Equal(Path.Combine(folder, "clip Snapshots"), output);
        var pictures = Directory.GetFiles(output).Order().ToList();
        Assert.Equal(Enumerable.Range(1, 5).Select(i => Path.Combine(output, $"Snapshot {i:00}.png")), pictures);
        // testsrc2 moves: frames from different moments differ.
        Assert.Equal(5, pictures.Select(p => Convert.ToHexString(ToolOperationTests.Hash(p))).Distinct().Count());
    }

    [Fact]
    public void Snapshots_are_spread_over_the_video()
    {
        Assert.Equal([5.0], ToolEncoding.SnapshotTimes(TimeSpan.FromSeconds(10), 1));
        Assert.Equal([1.0, 3, 5, 7, 9], ToolEncoding.SnapshotTimes(TimeSpan.FromSeconds(10), 5));
        Assert.Equal([0.0], ToolEncoding.SnapshotTimes(null, 10));
        Assert.Equal([2.5, 5, 7.5], ToolEncoding.SplitTimes(TimeSpan.FromSeconds(10), 4));
    }

    [Theory]
    [InlineData("mp3", 0, 1920, 1080)]
    [InlineData("wav", 1, 1080, 1920)]
    [InlineData("flac", 2, 1080, 1080)]
    public async Task Visualize_makes_an_mp4_of_the_waveform_with_the_sound(string format, int choice, int width, int height)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var tone = await MakeToneAsync(folder, "tone.wav", seconds: 3);
        var source = format == "wav" ? tone : Path.Combine(folder, "song." + format);
        if (format != "wav")
            await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-i", tone, source]);

        var output = await RunPickAsync("audioToVideo", choice, source);

        Assert.Equal(Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(source)} Visualizer.mp4"), output);
        var probed = await ProbeAsync(output);
        Assert.Equal(("h264", width, height), (probed.Video?.Codec, probed.Video?.Width, probed.Video?.Height));
        Assert.Equal("aac", probed.Audio?.Codec);
        Assert.InRange(probed.Duration, 2.8, 3.3);
    }
}
