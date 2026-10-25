// fork: the Bleep editor's ranges (Phase 5): parts of a song marked by dragging on its waveform, to be covered by a
// tone or silenced. Ranges that overlap or touch become one; every range is inside the song and at least a twentieth of
// a second long. Pure, unit tested (BleepRangesTests).

namespace Filee.Core.Editing;

/// <summary>What a bleeped range sounds like.</summary>
public enum BleepSound
{
    /// <summary>A 1 kHz tone, as on TV.</summary>
    Tone,

    /// <summary>Nothing.</summary>
    Silence,
}

public sealed class BleepRanges(double duration)
{
    /// <summary>The shortest range kept, in seconds.</summary>
    public const double MinLength = 0.05;

    private readonly List<TrimRange> _ranges = [];

    public double Duration { get; } = double.IsFinite(duration) && duration > 0 ? duration : 0;

    /// <summary>The ranges, in order, none overlapping.</summary>
    public IReadOnlyList<TrimRange> Ranges => _ranges;

    /// <summary>Marks <paramref name="from"/> … <paramref name="to"/> (either way round), joined with any it meets.</summary>
    /// <returns>The index of the range it is part of now, or −1 when it was too short.</returns>
    public int Add(double from, double to)
    {
        var (start, end) = (Math.Clamp(Math.Min(from, to), 0, Duration), Math.Clamp(Math.Max(from, to), 0, Duration));
        if (end - start < MinLength)
            return -1;
        var merged = new TrimRange(start, end);
        for (var i = _ranges.Count - 1; i >= 0; i--)
        {
            var range = _ranges[i];
            if (range.End < merged.Start || range.Start > merged.End)
                continue;
            merged = new TrimRange(Math.Min(range.Start, merged.Start), Math.Max(range.End, merged.End));
            _ranges.RemoveAt(i);
        }
        var at = _ranges.FindIndex(r => r.Start > merged.Start);
        at = at < 0 ? _ranges.Count : at;
        _ranges.Insert(at, merged);
        return at;
    }

    /// <summary>The range <paramref name="seconds"/> falls in, or −1.</summary>
    public int At(double seconds) => _ranges.FindIndex(r => seconds >= r.Start && seconds <= r.End);

    public bool Remove(int index)
    {
        if (index < 0 || index >= _ranges.Count)
            return false;
        _ranges.RemoveAt(index);
        return true;
    }

    public void Clear() => _ranges.Clear();
}
