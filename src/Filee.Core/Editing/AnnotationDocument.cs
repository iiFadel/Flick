// fork: the Annotate editor's marks (Phase 5): arrows, boxes, ellipses, text and numbered callouts on a picture, as
// fractions of it, with undo and redo. The window draws them on screen and the image engine into the PNG from the same
// geometry (AnnotationGeometry), so both look alike. Pure, unit tested (AnnotationDocumentTests).

namespace Filee.Core.Editing;

public enum AnnotationKind
{
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Callout,
}

/// <summary>
/// One mark. Arrow: from (X1, Y1) to its tip (X2, Y2). Rectangle and ellipse: two opposite corners. Text and callout:
/// placed at (X1, Y1) (text's top left, a callout's centre).
/// </summary>
/// <param name="Color">"#RRGGBB".</param>
public sealed record Annotation(AnnotationKind Kind, double X1, double Y1, double X2, double Y2, string Color, string Text = "")
{
    public bool IsPlaced => Kind is AnnotationKind.Text or AnnotationKind.Callout;
}

public sealed class AnnotationDocument
{
    /// <summary>A drag shorter than this (a fraction of the picture's side) draws no arrow, box or ellipse.</summary>
    public const double MinDrag = 0.01;

    private List<Annotation> _marks = [];
    private readonly Stack<List<Annotation>> _undo = new();
    private readonly Stack<List<Annotation>> _redo = new();

    public IReadOnlyList<Annotation> Marks => _marks;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>The number a callout shows: its place among the callouts, from 1, so removing one renumbers the rest.</summary>
    public int NumberOf(int index) =>
        index >= 0 && index < _marks.Count && _marks[index].Kind == AnnotationKind.Callout
            ? _marks.Take(index + 1).Count(m => m.Kind == AnnotationKind.Callout)
            : 0;

    /// <summary>Adds <paramref name="mark"/>; an arrow, box or ellipse dragged too short is not added.</summary>
    /// <returns>Its index, or −1.</returns>
    public int Add(Annotation mark)
    {
        if (!mark.IsPlaced && Math.Abs(mark.X2 - mark.X1) < MinDrag && Math.Abs(mark.Y2 - mark.Y1) < MinDrag)
            return -1;
        Change(marks => marks.Add(Clamp(mark)));
        return _marks.Count - 1;
    }

    /// <summary>Replaces the mark at <paramref name="index"/> (new text, colour); one step to undo.</summary>
    public void Replace(int index, Annotation mark)
    {
        if (index >= 0 && index < _marks.Count && _marks[index] != mark)
            Change(marks => marks[index] = Clamp(mark));
    }

    public bool Remove(int index)
    {
        if (index < 0 || index >= _marks.Count)
            return false;
        Change(marks => marks.RemoveAt(index));
        return true;
    }

    public bool Undo() => Step(_undo, _redo);

    public bool Redo() => Step(_redo, _undo);

    /// <summary>
    /// The topmost mark near the point (fractions of a <paramref name="width"/> × <paramref name="height"/> picture):
    /// inside a box, ellipse or text's area, near an arrow's line, on a callout's dot. A fiftieth of the shorter side
    /// off still counts.
    /// </summary>
    public int At(double x, double y, double width, double height)
    {
        var side = Math.Min(width, height);
        var slack = side * 0.02;
        var (px, py) = (x * width, y * height);
        for (var i = _marks.Count - 1; i >= 0; i--)
        {
            var m = _marks[i];
            var (x1, y1, x2, y2) = (m.X1 * width, m.Y1 * height, m.X2 * width, m.Y2 * height);
            var hit = m.Kind switch
            {
                AnnotationKind.Arrow => AnnotationGeometry.DistanceToSegment(px, py, x1, y1, x2, y2) <= slack,
                AnnotationKind.Callout => Math.Abs(px - x1) <= side * AnnotationGeometry.CalloutRadius + slack
                                          && Math.Abs(py - y1) <= side * AnnotationGeometry.CalloutRadius + slack,
                AnnotationKind.Text => px >= x1 - slack && py >= y1 - slack
                                       && px <= x1 + AnnotationGeometry.TextWidth(m.Text, side) + slack
                                       && py <= y1 + side * AnnotationGeometry.TextSize + slack,
                _ => px >= Math.Min(x1, x2) - slack && px <= Math.Max(x1, x2) + slack
                     && py >= Math.Min(y1, y2) - slack && py <= Math.Max(y1, y2) + slack,
            };
            if (hit)
                return i;
        }
        return -1;
    }

    private void Change(Action<List<Annotation>> change)
    {
        _undo.Push([.. _marks]);
        _redo.Clear();
        var next = new List<Annotation>(_marks);
        change(next);
        _marks = next;
    }

    private bool Step(Stack<List<Annotation>> from, Stack<List<Annotation>> to)
    {
        if (from.Count == 0)
            return false;
        to.Push(_marks);
        _marks = from.Pop();
        return true;
    }

    private static Annotation Clamp(Annotation m) =>
        m with { X1 = Math.Clamp(m.X1, 0, 1), Y1 = Math.Clamp(m.Y1, 0, 1), X2 = Math.Clamp(m.X2, 0, 1), Y2 = Math.Clamp(m.Y2, 0, 1) };
}

/// <summary>Sizes and shapes the window and the image engine both draw with, in pixels of the picture they draw on.</summary>
public static class AnnotationGeometry
{
    /// <summary>Line width as a fraction of the picture's shorter side.</summary>
    public const double Stroke = 0.007;

    /// <summary>Text height as a fraction of the picture's shorter side.</summary>
    public const double TextSize = 0.05;

    /// <summary>A callout dot's radius, a fraction of the shorter side.</summary>
    public const double CalloutRadius = 0.035;

    /// <summary>The two back corners of an arrow's head at (<paramref name="x2"/>, <paramref name="y2"/>), in pixels.</summary>
    public static ((double X, double Y) Left, (double X, double Y) Right) ArrowHead(double x1, double y1, double x2, double y2, double size)
    {
        var angle = Math.Atan2(y2 - y1, x2 - x1);
        const double spread = Math.PI / 7;
        return ((x2 - size * Math.Cos(angle - spread), y2 - size * Math.Sin(angle - spread)),
                (x2 - size * Math.Cos(angle + spread), y2 - size * Math.Sin(angle + spread)));
    }

    /// <summary>The arrow head's length for a picture whose shorter side is <paramref name="shortSide"/> pixels.</summary>
    public static double HeadSize(double shortSide) => Math.Max(10, shortSide * Stroke * 5);

    /// <summary>A rough width of <paramref name="text"/> on a picture whose shorter side is <paramref name="shortSide"/> (for picking it).</summary>
    public static double TextWidth(string text, double shortSide) => Math.Max(1, text.Length) * shortSide * TextSize * 0.55;

    /// <summary>Distance from a point to a segment.</summary>
    public static double DistanceToSegment(double x, double y, double x1, double y1, double x2, double y2)
    {
        var (dx, dy) = (x2 - x1, y2 - y1);
        var length = dx * dx + dy * dy;
        var t = length <= 0 ? 0 : Math.Clamp(((x - x1) * dx + (y - y1) * dy) / length, 0, 1);
        var (cx, cy) = (x1 + t * dx, y1 + t * dy);
        return Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
    }
}
