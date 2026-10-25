// fork: the video and audio editor windows' edits (Phase 5) with the real FFmpeg (skipped where it is not installed), and
// what the windows show before anything is written (MediaPreview).

using Filee.Core.Conversion;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Core.Wheels;
using Filee.Engines.Media;

namespace Filee.Engines.Tests;

public partial class MediaTests
{
    private static Preset Edited(string tool, string format, MediaEdit edit)
    {
        var preset = ToolOperations.For(tool, [format], id => id switch
        {
            "cropVideo" => "Cropped",
            "redactVideo" => "Redacted",
            "redactAudio" => "Bleeped",
            _ => "Trimmed",
        }) ?? throw new InvalidOperationException($"No {tool} for {format}.");
        preset.Media.Edit = edit;
        return preset;
    }

    private async Task<string> RunEditAsync(string tool, string source, MediaEdit edit)
    {
        var before = ToolOperationTests.Hash(source);
        var job = await fx.ConvertAsync([source], Edited(tool, Path.GetExtension(source).TrimStart('.'), edit));
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.Equal(before, ToolOperationTests.Hash(source));
        return Assert.Single(job.Outputs);
    }

    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.mov")]
    [InlineData("clip.mkv")]
    public async Task Trim_keeps_the_part_between_the_handles_of_a_video(string name)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        // One keyframe for the whole clip: a stream copy could not start at 1.5 s, the re-encode must.
        var source = await MakeClipAsync(folder, name, seconds: 6, extra: ["-c:v", "libx264", "-g", "300", "-pix_fmt", "yuv420p", "-c:a", "aac"]);

        var output = await RunEditAsync("trimVideo", source, new MediaEdit { Trim = new TrimRange(1.5, 4) });

        Assert.Equal(Path.Combine(folder, "clip Trimmed" + Path.GetExtension(name)), output);
        var probed = await ProbeAsync(output);
        Assert.InRange(probed.Duration, 2.3, 2.7);
        Assert.NotNull(probed.Video);
        Assert.NotNull(probed.Audio);
    }

    [Theory]
    [InlineData("song.mp3")]
    [InlineData("song.m4a")]
    [InlineData("song.wav")]
    [InlineData("song.flac")]
    public async Task Trim_keeps_the_part_between_the_handles_of_a_song(string name)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeToneAsync(folder, name, seconds: 5);

        var output = await RunEditAsync("trimAudio", source, new MediaEdit { Trim = new TrimRange(0.5, 2) });

        Assert.Equal(Path.Combine(folder, "song Trimmed" + Path.GetExtension(name)), output);
        Assert.InRange((await ProbeAsync(output)).Duration, 1.4, 1.65);
    }

    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.mkv")]
    public async Task Crop_keeps_the_part_chosen_of_every_frame_with_even_sides(string name)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, name, seconds: 2, size: "640x360");

        var output = await RunEditAsync("cropVideo", source, new MediaEdit { Crop = new NormalizedRect(0.1, 0.2, 0.333, 0.5) });

        Assert.Equal(Path.Combine(folder, "clip Cropped" + Path.GetExtension(name)), output);
        var probed = await ProbeAsync(output);
        Assert.Equal((212, 180), (probed.Video!.Width, probed.Video.Height)); // 213.1 and 180, rounded down to even
        Assert.NotNull(probed.Audio);
        Assert.InRange(probed.Duration, 1.8, 2.2);
    }

    [Fact]
    public async Task Trim_and_crop_together_do_both()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mov", seconds: 4, size: "320x240");

        var output = await RunEditAsync("cropVideo", source,
            new MediaEdit { Crop = new NormalizedRect(0, 0, 0.5, 0.5), Trim = new TrimRange(1, 2) });

        var probed = await ProbeAsync(output);
        Assert.Equal((160, 120), (probed.Video!.Width, probed.Video.Height));
        Assert.InRange(probed.Duration, 0.8, 1.2);
    }

    [Theory]
    [InlineData(RedactStyle.Solid)]
    [InlineData(RedactStyle.Blur)]
    public async Task Redact_hides_boxes_on_every_frame_of_a_video(RedactStyle style)
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var source = await MakeClipAsync(folder, "clip.mp4", seconds: 2, size: "640x360");
        var boxes = new[] { new NormalizedRect(0, 0, 0.25, 0.25), new NormalizedRect(0.5, 0.5, 0.3, 0.3) };

        var output = await RunEditAsync("redactVideo", source, new MediaEdit { Redactions = boxes, RedactStyle = style });

        Assert.Equal(Path.Combine(folder, "clip Redacted.mp4"), output);
        var probed = await ProbeAsync(output);
        Assert.Equal((640, 360), (probed.Video!.Width, probed.Video.Height));
        Assert.InRange(probed.Duration, 1.8, 2.2);

        // The last frame: the first box is black, or has lost the test pattern's sharp edges.
        var frame = Path.Combine(folder, "frame.png");
        await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-sseof", "-0.2", "-i", output, "-frames:v", "1", "-update", "1", frame]);
        using var picture = new ImageMagick.MagickImage(frame);
        using var box = picture.CloneArea(8, 8, 140, 70);
        if (style == RedactStyle.Solid)
            Assert.True(box.Statistics().Composite().Maximum < 0.1 * ImageMagick.Quantum.Max, "the box is black");
        else
        {
            var sourceFrame = Path.Combine(folder, "source.png");
            await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-sseof", "-0.2", "-i", source, "-frames:v", "1", "-update", "1", sourceFrame]);
            using var original = new ImageMagick.MagickImage(sourceFrame);
            using var originalBox = original.CloneArea(8, 8, 140, 70);
            var (blurred, sharp) = (box.Statistics().Composite().StandardDeviation, originalBox.Statistics().Composite().StandardDeviation);
            var (edgesAfter, edgesBefore) = (Edges(box), Edges(originalBox));
            Assert.True(edgesAfter < edgesBefore * 0.3, $"the box is blurred: edges {edgesAfter} (was {edgesBefore}), deviation {blurred} (was {sharp})");
        }
    }

    /// <summary>How much sharp detail a picture has: the mean of its edge image.</summary>
    private static double Edges(ImageMagick.IMagickImage<byte> picture)
    {
        using var edges = picture.Clone();
        edges.Grayscale();
        edges.Edge(1);
        return edges.Statistics().Composite().Mean;
    }

    [Fact]
    public void The_crop_filter_is_relative_to_the_input_and_even() =>
        Assert.Equal("crop=w='max(2,trunc(iw*0.5/2)*2)':h='max(2,trunc(ih*0.25/2)*2)':x='trunc(iw*0.1/2)*2':y='trunc(ih*0/2)*2'",
            MediaEdits.CropFilter(new NormalizedRect(0.1, -1, 0.5, 0.25)));

    [Fact]
    public async Task The_preview_reads_the_length_frames_peaks_and_silence()
    {
        SkipUnlessInstalled();
        var folder = fx.NewFolder();
        var clip = await MakeClipAsync(folder, "clip.mp4", seconds: 3);
        // 1 s of silence, 1 s of tone, 1 s of silence.
        var song = Path.Combine(folder, "quiet.wav");
        await RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-f", "lavfi",
            "-i", "aevalsrc='if(between(t,1,2),0.5*sin(2*PI*440*t),0)':s=8000:d=3", song]);

        var summary = await MediaPreview.ProbeAsync(clip, CancellationToken.None);
        Assert.InRange(summary.Duration, 2.9, 3.1);
        Assert.Equal(25, summary.FrameRate, 1);
        Assert.True(summary.HasVideo && summary.HasAudio);
        Assert.Equal((320, 240), (summary.Width, summary.Height));

        var frames = await MediaPreview.FramesAsync(clip, summary.Duration, 6, 48, Directory.CreateDirectory(Path.Combine(folder, "strip")).FullName, CancellationToken.None);
        Assert.InRange(frames.Count, 5, 6);

        var peaks = await MediaPreview.PeaksAsync(song, 30, Path.Combine(folder, "song.pcm"), CancellationToken.None);
        Assert.Equal(30, peaks.Length);
        Assert.True(peaks[3] < 0.01 && peaks[15] > 0.4 && peaks[27] < 0.01);

        var silences = await MediaPreview.SilencesAsync(song, 3, CancellationToken.None);
        Assert.Equal(new TrimRange(1, 2), TrimMath.WithoutSilentEnds(silences, 3) is var kept
            ? new TrimRange(Math.Round(kept.Start, 1), Math.Round(kept.End, 1)) : default);
    }

    [Fact]
    public void Peaks_are_the_loudest_sample_of_each_part()
    {
        short[] samples = [0, 100, -16384, 50, 32767, 0, 0, -8192];
        var pcm = samples.SelectMany(s => BitConverter.GetBytes(s)).ToArray();

        var peaks = MediaPreview.Peaks(pcm, 4);

        Assert.Equal([100 / 32768f, 0.5f, 32767 / 32768f, 0.25f], peaks);
        Assert.Empty(MediaPreview.Peaks([], 0));
        Assert.Equal([0f, 0f], MediaPreview.Peaks([], 2));
    }

    [Fact]
    public void Silences_are_read_from_silencedetects_log()
    {
        const string log = """
            [silencedetect @ 0x1] silence_start: -0.002
            [silencedetect @ 0x1] silence_end: 1.004 | silence_duration: 1.006
            size=N/A time=00:00:02.00
            [silencedetect @ 0x1] silence_start: 2.5
            """;

        Assert.Equal([(0.0, 1.004), (2.5, 3.0)], MediaPreview.Silences(log, 3));
    }
}
