// fork: the Tools wheel's video and audio operations that are more than options on an ordinary encode
// (docs/TANGERINE-SPEC.md, "Tools wheel"): Speed is a filter on MediaEncoding's video encode; Split, Snapshot and
// Visualize are runs of their own, taken by FfmpegConverter when the preset asks for them. All of them run on the
// self edge (or video → PNG for Snapshot), so the route planner needs nothing new.

using System.Globalization;
using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines.Archives;

namespace Filee.Engines.Media;

internal static class ToolEncoding
{
    /// <summary>Tangerine's orange (the selected petal), the Visualizer's waveform colour.</summary>
    private const string Orange = "0xFF5200";

    /// <summary><see cref="MediaOptions.Speed"/>, or 1 where it is not a usable speed.</summary>
    public static double Speed(MediaOptions options) =>
        double.IsFinite(options.Speed) && options.Speed is >= 0.25 and <= 4 ? options.Speed : 1;

    /// <summary>The start of a video filter chain that plays <see cref="MediaOptions.Speed"/> times faster ("" at 1×).</summary>
    public static string SpeedFilter(MediaOptions options) =>
        Speed(options) is var speed && speed != 1 ? $"setpts=PTS/{Number(speed)}," : "";

    /// <summary>
    /// The sound at <see cref="MediaOptions.Speed"/>, its pitch kept. atempo takes 0.5 … 100 per filter, so 0.25× is
    /// two 0.5× filters.
    /// </summary>
    public static IEnumerable<string> TempoArguments(MediaOptions options)
    {
        var speed = Speed(options);
        if (speed == 1)
            return [];
        var chain = speed < 0.5 ? $"atempo=0.5,atempo={Number(speed / 0.5)}" : $"atempo={Number(speed)}";
        return ["-af", chain];
    }

    /// <summary>
    /// Runs Split, Snapshot or Visualize when <paramref name="step"/>'s preset asks for one of them; null for an
    /// ordinary conversion.
    /// </summary>
    public static async Task<IReadOnlyList<string>?> TryRunAsync(string ffmpeg, ConversionStep step, string input, MediaInfo info,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var media = step.Preset.Media;
        if (media.Snapshots > 0 && step.To == "png")
            return await SnapshotsAsync(ffmpeg, step, input, info, progress, cancellationToken);
        if (media.SplitParts > 1 && MediaEncoding.VideoTargets.ContainsKey(step.To))
            return await SplitAsync(ffmpeg, step, input, info, progress, cancellationToken);
        if (media.Visualizer != VisualizerShape.None && MediaEncoding.AudioTargets.ContainsKey(step.To))
            return await VisualizeAsync(ffmpeg, step, input, info, progress, cancellationToken);
        return null;
    }

    // ───────────────────────── Snapshot ─────────────────────────

    /// <summary>
    /// Where the <paramref name="count"/> snapshots are taken: spread evenly, each in the middle of its share of the
    /// video (one is the middle frame). Only the first frame when the length is unknown.
    /// </summary>
    internal static IReadOnlyList<double> SnapshotTimes(TimeSpan? duration, int count) =>
        duration is { TotalSeconds: > 0 } length
            ? Enumerable.Range(0, Math.Max(1, count)).Select(i => length.TotalSeconds * (i + 0.5) / Math.Max(1, count)).ToList()
            : [0];

    /// <summary>One full-size PNG frame: "clip Snapshot.png", or several in "clip Snapshots" (Snapshot 01.png, …).</summary>
    private static async Task<IReadOnlyList<string>> SnapshotsAsync(string ffmpeg, ConversionStep step, string input, MediaInfo info,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var video = info.Video ?? throw new InvalidOperationException("The file has no video stream.");
        var times = SnapshotTimes(info.Duration, step.Preset.Media.Snapshots);
        if (step.Preset.Media.Snapshots == 1)
        {
            var output = step.Output.Allocate("png");
            if (output is null)
                return [];
            await RunAsync(ffmpeg, SnapshotArguments(input, output, video, times[0]), null, progress, cancellationToken);
            return [output];
        }

        return await IntoFolderAsync(step, async folder =>
        {
            for (var i = 0; i < times.Count; i++)
            {
                var path = Path.Combine(folder, $"Snapshot {i + 1:00}.png");
                await RunAsync(ffmpeg, SnapshotArguments(input, path, video, times[i]), null, null, cancellationToken);
                progress?.Report(0.05 + 0.9 * (i + 1) / times.Count);
            }
        });
    }

    internal static List<string> SnapshotArguments(string input, string output, VideoStreamInfo video, double seconds) =>
    [
        .. MediaEncoding.Common, "-ss", Number(seconds), "-i", input, "-map", "0:V:0",
        // Square pixels, as the still frame of a conversion has.
        "-vf", Math.Abs(video.SampleAspect - 1) > 0.01 ? "scale=w='trunc(iw*sar/2)*2':h=ih,setsar=1" : "null",
        "-frames:v", "1", "-update", "1", "-c:v", "png", "-f", "image2", output,
    ];

    // ───────────────────────── Split ─────────────────────────

    /// <summary>Where the video is cut into <paramref name="parts"/> of equal length (the cut falls on the next keyframe).</summary>
    internal static IReadOnlyList<double> SplitTimes(TimeSpan duration, int parts) =>
        Enumerable.Range(1, parts - 1).Select(i => duration.TotalSeconds * i / parts).ToList();

    /// <summary>
    /// The video cut into <see cref="MediaOptions.SplitParts"/> clips at keyframes, streams copied, in the folder
    /// "clip Clips" (clip 1.mp4, clip 2.mp4, …).
    /// </summary>
    private static async Task<IReadOnlyList<string>> SplitAsync(string ffmpeg, ConversionStep step, string input, MediaInfo info,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (info.Video is null)
            throw new InvalidOperationException("The file has no video stream.");
        if (info.Duration is not { TotalSeconds: > 0 } duration)
            throw new InvalidOperationException("The length of the video is unknown.");
        var extension = FormatRegistry.Get(step.To).PrimaryExtension;
        var name = FormatRegistry.NameWithoutExtension(step.InputPath);
        return await IntoFolderAsync(step, folder =>
        {
            // FFmpeg reads % in the whole path as part of the number pattern: a literal one is written %%.
            var pattern = Path.Combine(folder.Replace("%", "%%", StringComparison.Ordinal), $"{name.Replace("%", "%%", StringComparison.Ordinal)} %d.{extension}");
            return RunAsync(ffmpeg, SplitArguments(input, pattern, MediaEncoding.VideoTargets[step.To].Muxer, SplitTimes(duration, step.Preset.Media.SplitParts)),
                duration, progress, cancellationToken);
        });
    }

    internal static List<string> SplitArguments(string input, string pattern, string muxer, IReadOnlyList<double> times) =>
    [
        .. MediaEncoding.Common, "-i", input, "-map", "0:V:0", "-map", "0:a?", "-c", "copy",
        "-f", "segment", "-segment_format", muxer, "-segment_times", string.Join(",", times.Select(Number)),
        "-reset_timestamps", "1", "-segment_start_number", "1", "-progress", "pipe:1", pattern,
    ];

    // ───────────────────────── Visualize ─────────────────────────

    internal static (int Width, int Height) FrameOf(VisualizerShape shape) => shape switch
    {
        VisualizerShape.Portrait => (1080, 1920),
        VisualizerShape.Square => (1080, 1080),
        _ => (1920, 1080),
    };

    /// <summary>The song as "song Visualizer.mp4": an orange waveform across the middle of a black frame, with the sound.</summary>
    private static async Task<IReadOnlyList<string>> VisualizeAsync(string ffmpeg, ConversionStep step, string input, MediaInfo info,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (info.Audio is null)
            throw new InvalidOperationException("The file has no audio stream.");
        var output = step.Output.Allocate("mp4");
        if (output is null)
            return [];
        try
        {
            await RunAsync(ffmpeg, VisualizerArguments(input, output, step.Preset.Media.Visualizer), info.Duration, progress, cancellationToken);
        }
        catch
        {
            File.Delete(output);
            throw;
        }
        return [output];
    }

    internal static List<string> VisualizerArguments(string input, string output, VisualizerShape shape)
    {
        var (width, height) = FrameOf(shape);
        var wave = height / 3 / 2 * 2;
        return
        [
            .. MediaEncoding.Common, "-i", input,
            "-filter_complex", $"[0:a:0]showwaves=s={width}x{wave}:mode=cline:draw=full:rate=30:colors={Orange}:scale=sqrt,"
                + $"pad={width}:{height}:0:(oh-ih)/2:black,format=yuv420p[v]",
            "-map", "[v]", "-map", "0:a:0",
            "-c:v", MediaEncoding.VideoTargets["mp4"].VideoCodec!, "-preset", "medium", "-crf", "23",
            "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart",
            "-f", "mp4", "-progress", "pipe:1", output,
        ];
    }

    // ───────────────────────── Helpers ─────────────────────────

    /// <summary>
    /// Writes into a hidden folder next to the output folder the rule names, moved into place once
    /// <paramref name="write"/> is done, so a cancelled or failed run leaves nothing behind (as FrameSequenceWriter).
    /// </summary>
    private static async Task<IReadOnlyList<string>> IntoFolderAsync(ConversionStep step, Func<string, Task> write)
    {
        var output = step.Output.Allocate("");
        if (output is null)
            return [];
        var staging = Path.Combine(Path.GetDirectoryName(output)!, $".{Path.GetFileName(output)}.filee-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(staging).Attributes |= FileAttributes.Hidden;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(staging, "out")).FullName;
            await write(folder);
            if (!Directory.EnumerateFiles(folder).Any())
                throw new InvalidOperationException("FFmpeg did not write anything.");
            ArchiveFiles.MoveFolder(folder, output);
            return [output];
        }
        finally
        {
            ArchiveFiles.TryDeleteFolder(staging);
        }
    }

    private static Task RunAsync(string ffmpeg, List<string> arguments, TimeSpan? duration, IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        FfmpegConverter.RunPassAsync(ffmpeg, new FfmpegPass(arguments, 1), duration, progress, 0, cancellationToken);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
