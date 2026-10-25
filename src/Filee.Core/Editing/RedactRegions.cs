// fork: the Redact editor's boxes (Phase 5): drawn by dragging over the picture, as fractions of it; too small a drag
// draws nothing, a click on a box picks it, the last one drawn can be taken back. Pure, unit tested (RedactRegionsTests).

namespace Filee.Core.Editing;

/// <summary>How a box hides what is under it.</summary>
public enum RedactStyle
{
    /// <summary>Filled black.</summary>
    Solid,

    /// <summary>Blurred beyond reading.</summary>
    Blur,
}

public sealed class RedactRegions
{
    /// <summary>A drag shorter than this (a fraction of the picture's side) is a click, not a box.</summary>
    public const double MinSide = 0.01;

    private readonly List<NormalizedRect> _boxes = [];

    public IReadOnlyList<NormalizedRect> Boxes => _boxes;

    /// <summary>The box from one corner to the other, inside the picture; null (nothing drawn) for a click.</summary>
    public static NormalizedRect? Box(double x1, double y1, double x2, double y2)
    {
        var (left, right) = (Math.Clamp(Math.Min(x1, x2), 0, 1), Math.Clamp(Math.Max(x1, x2), 0, 1));
        var (top, bottom) = (Math.Clamp(Math.Min(y1, y2), 0, 1), Math.Clamp(Math.Max(y1, y2), 0, 1));
        return right - left < MinSide || bottom - top < MinSide ? null : new NormalizedRect(left, top, right - left, bottom - top);
    }

    /// <summary>Adds the box dragged from (<paramref name="x1"/>, <paramref name="y1"/>) to the other corner.</summary>
    /// <returns>Its index, or −1 when the drag was too small.</returns>
    public int Add(double x1, double y1, double x2, double y2)
    {
        if (Box(x1, y1, x2, y2) is not { } box)
            return -1;
        _boxes.Add(box);
        return _boxes.Count - 1;
    }

    /// <summary>The topmost (last drawn) box under the point, or −1.</summary>
    public int At(double x, double y)
    {
        for (var i = _boxes.Count - 1; i >= 0; i--)
        {
            var b = _boxes[i];
            if (x >= b.X && x <= b.X + b.Width && y >= b.Y && y <= b.Y + b.Height)
                return i;
        }
        return -1;
    }

    public bool Remove(int index)
    {
        if (index < 0 || index >= _boxes.Count)
            return false;
        _boxes.RemoveAt(index);
        return true;
    }

    /// <summary>Takes back the last box drawn.</summary>
    public bool Undo() => Remove(_boxes.Count - 1);

    public void Clear() => _boxes.Clear();
}
