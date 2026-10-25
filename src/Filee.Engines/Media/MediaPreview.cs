// fork: what the video and audio editor windows (Phase 5) show before anything is written: the file's length, a strip
// of small frames, one frame at a time while a handle is dragged, the waveform's peaks, and where it is silent. Every
// run writes into a folder the caller owns and deletes.

using System.Globalization;
using System.Text.RegularExpressions;
using Filee.Engines.Infrastructure;

namespace Filee.Engines.Media;

/// <summary>A video or song as an editor window shows it.</summary>
/// <param name="Duration">Seconds.</param>
/// <param name="FrameRate">Frames per second, 0 for audio (or when unknown).</param>
internal sealed record MediaSummary(double Duration, double FrameRate, bool HasVideo, bool HasAudio);

internal static partial class MediaPreview
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>FFmpeg and FFprobe; throws when FFmpeg is not installed.</summary>
    private static (string Ffmpeg, string Ffprobe) Tools =>
        FfmpegConverter.Locate() ?? throw new InvalidOperationException("FFmpeg was not found.");

    public static async Task<MediaSummary> ProbeAsync(string input, CancellationToken cancellationToken)
    {
        var info = await MediaInfo.ProbeAsync(Tools.Ffprobe, input, cancellationToken);
        if (info.Duration is not { TotalSeconds: > 0 } duration)
            throw new NotSupportedException("The length of the file is unknown.");
        return new MediaSummary(duration.TotalSeconds, info.Video?.FrameRate ?? 0, info.Video is not null, info.Audio is not null);
    }

    /// <summary>
    /// <paramref name="count"/> frames spread over the video, <paramref name="height"/> pixels high, as PNGs in
    /// <paramref name="folder"/> (frame 01.png, …), in order.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FramesAsync(string input, double duration, int count, int height, string folder,
        CancellationToken cancellationToken)
    {
        var rate = (count / Math.Max(0.1, duration)).ToString("0.######", CultureInfo.InvariantCulture);
        await RunAsync(Tools.Ffmpeg,
        [
            .. MediaEncoding.Common, "-i", input, "-map", "0:V:0",
            "-vf", $"fps={rate},scale=-2:{height}:flags=fast_bilinear,setsar=1", "-frames:v", count.ToString(CultureInfo.InvariantCulture),
            "-c:v", "png", "-f", "image2", Path.Combine(folder, "frame %02d.png"),
        ], cancellationToken);
        return [.. Directory.GetFiles(folder, "frame *.png").Order(StringComparer.Ordinal)];
    }

    /// <summary>The frame at <paramref name="seconds"/>, <paramref name="height"/> pixels high, written to <paramref name="output"/>.</summary>
    public static Task FrameAtAsync(string input, double seconds, int height, string output, CancellationToken cancellationToken) =>
        RunAsync(Tools.Ffmpeg,
        [
            .. MediaEncoding.Common, "-ss", Number(seconds), "-i", input, "-map", "0:V:0",
            "-vf", $"scale=-2:{height},setsar=1", "-frames:v", "1", "-update", "1", "-c:v", "png", "-f", "image2", output,
        ], cancellationToken);

    /// <summary>
    /// The loudest sample (0 … 1) in each of <paramref name="count"/> equal parts of the sound, from a mono 8 kHz copy
    /// written to <paramref name="scratch"/>.
    /// </summary>
    public static async Task<float[]> PeaksAsync(string input, int count, string scratch, CancellationToken cancellationToken)
    {
        await RunAsync(Tools.Ffmpeg, [.. MediaEncoding.Common, "-i", input, "-map", "0:a:0", "-ac", "1", "-ar", "8000", "-f", "s16le", "-c:a", "pcm_s16le", scratch],
            cancellationToken);
        return Peaks(await File.ReadAllBytesAsync(scratch, cancellationToken), count);
    }

    /// <summary>The loudest sample of each of <paramref name="count"/> parts of 16-bit little-endian mono PCM.</summary>
    internal static float[] Peaks(ReadOnlySpan<byte> pcm, int count)
    {
        var peaks = new float[Math.Max(0, count)];
        var samples = pcm.Length / 2;
        if (samples == 0 || count <= 0)
            return peaks;
        for (var i = 0; i < samples; i++)
        {
            var value = Math.Abs((int)(short)(pcm[2 * i] | pcm[2 * i + 1] << 8)) / 32768f;
            var bucket = (int)((long)i * count / samples);
            if (value > peaks[bucket])
                peaks[bucket] = value;
        }
        return peaks;
    }

    /// <summary>Where the sound is quieter than −50 dB for at least half a second (FFmpeg's silencedetect).</summary>
    public static async Task<IReadOnlyList<(double Start, double End)>> SilencesAsync(string input, double duration, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(Tools.Ffmpeg,
            ["-hide_banner", "-nostdin", "-nostats", "-v", "info", "-i", input, "-map", "0:a:0", "-af", "silencedetect=noise=-50dB:d=0.5", "-f", "null", "-"],
            Timeout, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("FFmpeg could not read the sound: " + FfmpegRunner.Summarize(result.StandardError));
        return Silences(result.StandardError, duration);
    }

    /// <summary>The spans in silencedetect's log; a silence still going at the end ends at <paramref name="duration"/>.</summary>
    internal static IReadOnlyList<(double Start, double End)> Silences(string log, double duration)
    {
        var spans = new List<(double, double)>();
        double? start = null;
        foreach (Match match in SilenceLine().Matches(log))
        {
            var seconds = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (match.Groups[1].Value == "start")
                start = Math.Max(0, seconds);
            else if (start is { } from)
            {
                spans.Add((from, seconds));
                start = null;
            }
        }
        if (start is { } open)
            spans.Add((open, duration));
        return spans;
    }

    [GeneratedRegex(@"silence_(start|end): (-?[0-9.]+)")]
    private static partial Regex SilenceLine();

    private static async Task RunAsync(string ffmpeg, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await ProcessRunner.RunAsync(ffmpeg, arguments, Timeout, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("FFmpeg failed: " + FfmpegRunner.Summarize(result.StandardError));
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
