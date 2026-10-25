// fork: the Trim editor's timeline (Phase 5): two handles on a file of known length, moved by dragging, by a frame
// (video) or a tenth of a second (audio), or to where the silence at the ends stops. Pure, unit tested (TrimMathTests).

namespace Filee.Core.Editing;

/// <summary>The part of a recording to keep, in seconds from its start.</summary>
public readonly record struct TrimRange(double Start, double End)
{
    public double Length => End - Start;
}

public static class TrimMath
{
    /// <summary>The shortest part Trim keeps, in seconds.</summary>
    public const double MinLength = 0.1;

    /// <summary>The step of the arrow keys: one frame at <paramref name="frameRate"/>, a tenth of a second for audio.</summary>
    public static double Step(double frameRate) => double.IsFinite(frameRate) && frameRate > 0 ? 1 / frameRate : 0.1;

    /// <summary>
    /// <paramref name="range"/> inside 0 … <paramref name="duration"/> and at least <see cref="MinLength"/> long (or the
    /// whole recording when it is shorter); a value that isn't a number counts as the nearest end.
    /// </summary>
    public static TrimRange Clamp(TrimRange range, double duration)
    {
        duration = double.IsFinite(duration) && duration > 0 ? duration : 0;
        var min = Math.Min(MinLength, duration);
        var start = Math.Clamp(Finite(range.Start, 0), 0, duration - min);
        var end = Math.Clamp(Finite(range.End, duration), start + min, duration);
        return new TrimRange(start, end);
    }

    /// <summary>The start handle moved to <paramref name="seconds"/>; it stops <see cref="MinLength"/> before the end.</summary>
    public static TrimRange MoveStart(TrimRange range, double seconds, double duration) =>
        Clamp(range with { Start = Math.Min(seconds, range.End - MinLength) }, duration);

    /// <summary>The end handle moved to <paramref name="seconds"/>; it stops <see cref="MinLength"/> after the start.</summary>
    public static TrimRange MoveEnd(TrimRange range, double seconds, double duration) =>
        Clamp(range with { End = Math.Max(seconds, range.Start + MinLength) }, duration);

    /// <summary>True when <paramref name="range"/> keeps the whole recording (to within a millisecond).</summary>
    public static bool IsWhole(TrimRange range, double duration) => range.Start < 0.001 && range.End > duration - 0.001;

    /// <summary>
    /// The range without the silence at both ends: <paramref name="silences"/> are FFmpeg's silencedetect spans (start,
    /// end in seconds). A silence that starts at 0 moves the start to its end, one that reaches the end moves the end
    /// to its start; silence in the middle stays. A recording that is all silence is kept whole.
    /// </summary>
    public static TrimRange WithoutSilentEnds(IReadOnlyList<(double Start, double End)> silences, double duration, double tolerance = 0.05)
    {
        var start = 0.0;
        var end = duration;
        foreach (var (from, to) in silences.OrderBy(s => s.Start))
        {
            if (from <= start + tolerance && to > start)
                start = Math.Min(to, duration);
        }
        foreach (var (from, to) in silences.OrderByDescending(s => s.End))
        {
            if (to >= end - tolerance && from < end)
                end = Math.Max(from, 0);
        }
        return end - start < MinLength ? new TrimRange(0, duration) : Clamp(new TrimRange(start, end), duration);
    }

    /// <summary>"1:02.5" (minutes, seconds, tenths); hours when there are any ("1:00:02.5").</summary>
    public static string Format(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, Math.Round(Finite(seconds, 0), 1)));
        var tenths = time.Milliseconds / 100;
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{tenths}"
            : $"{(int)time.TotalMinutes}:{time.Seconds:00}.{tenths}";
    }

    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
}
