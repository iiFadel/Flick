// fork: Tangerine's wheel geometry, ported from its website's demo (we() and Ke(), docs/TANGERINE-SPEC.md). Pure math,
// unit tested (PetalGeometryTests).
//
// Slices ("petals") are separated by a gap of constant width, have rounded corners drawn with quadratic curves, and
// start at the top going clockwise; a wheel of three starts on the left. Internally the angles are the site's (radians
// counter-clockwise from 3 o'clock); the public angles are DonutGeometry's (clockwise from 12 o'clock).

using Avalonia;

namespace Filee.App.Controls;

/// <summary>
/// Radii of a wheel. Tangerine's own wheel has <see cref="SiteOuter"/> as its outer radius; any other size keeps its
/// proportions.
/// </summary>
/// <param name="Inner">Where the petals start.</param>
/// <param name="Outer">Where the petals end.</param>
/// <param name="Backdrop">Outer edge of the track behind the petals.</param>
/// <param name="Hole">Inner edge of the track behind the petals.</param>
/// <param name="Gap">Width of the gap between two petals.</param>
/// <param name="Corner">How far the outer corners' rounding reaches along a petal's side.</param>
/// <param name="LabelRadius">Where a petal's label (or icon and label) is centred.</param>
public sealed record PetalLayout(double Inner, double Outer, double Backdrop, double Hole, double Gap, double Corner, double LabelRadius)
{
    /// <summary>Outer radius of Tangerine's wheel, in pixels.</summary>
    public const double SiteOuter = 166 * 0.8 * 0.9;

    /// <summary>Tangerine's proportions for a wheel whose petals end at <paramref name="outer"/>.</summary>
    public static PetalLayout ForOuter(double outer)
    {
        var s = outer / SiteOuter;
        const double labelInner = 50 * 0.8;
        return new PetalLayout(
            Inner: (48 * 0.8 * 1.1 + 8 * 0.8) * s,
            Outer: outer,
            Backdrop: (SiteOuter + 8 * 0.8) * s,
            Hole: 48 * 0.8 * 1.1 * s,
            Gap: 5 * 0.8 * s,
            Corner: 28 * 0.8 * 0.9 * s,
            LabelRadius: (labelInner + (SiteOuter - labelInner) * 0.56) * s);
    }
}

public enum PetalStepKind { Line, Quad, Arc }

/// <summary>
/// One step of a petal's outline: a line to <paramref name="To"/>, a quadratic curve through <paramref name="Control"/>,
/// or an arc of <paramref name="ArcRadius"/> around the wheel's centre.
/// </summary>
public readonly record struct PetalStep(PetalStepKind Kind, Point To, Point Control = default, double ArcRadius = 0,
    bool IsLargeArc = false, bool Clockwise = false);

/// <summary>A closed petal outline starting at <paramref name="Start"/>.</summary>
public sealed record PetalOutline(Point Start, IReadOnlyList<PetalStep> Steps);

public static class PetalGeometry
{
    /// <summary>Centre angle of petal <paramref name="index"/> of <paramref name="count"/>, clockwise from 12 o'clock (0..2π).</summary>
    public static double MidAngle(int index, int count)
    {
        var a = Math.PI / 2 - SiteAngle(index, count);
        a %= 2 * Math.PI;
        return a < 0 ? a + 2 * Math.PI : a;
    }

    /// <summary>Where petal <paramref name="index"/>'s label is centred.</summary>
    public static Point LabelPoint(Point center, PetalLayout layout, int index, int count) =>
        At(center, layout.LabelRadius, SiteAngle(index, count));

    /// <summary>Outline of petal <paramref name="index"/> of <paramref name="count"/> (the site's path, step for step).</summary>
    public static PetalOutline Petal(Point center, PetalLayout layout, int index, int count) =>
        Petal(center, layout, MidAngle(index, count), 2 * Math.PI / count);

    /// <summary>
    /// Outline of a petal <paramref name="span"/> radians wide centred on <paramref name="mid"/> (clockwise from 12
    /// o'clock), for slices that slide while the toolbar editor makes room.
    /// </summary>
    public static PetalOutline Petal(Point center, PetalLayout layout, double mid, double span)
    {
        var (inner, outer) = (layout.Inner, layout.Outer);
        var a = Math.PI / 2 - mid;
        var (l, s) = (a - span / 2, a + span / 2);
        double R(double radius) => Math.Asin(Math.Min(0.95, layout.Gap / 2 / Math.Max(radius, layout.Gap / 2 + 0.01)));

        var (d, i) = (l + R(outer), s - R(outer));
        var (c, g) = (l + R(inner), s - R(inner));
        var f = Math.Min(0.15, (i - d) * 0.24);
        var u = Math.Min(0.18, (g - c) * 0.18);
        var v = inner / Math.Max(0.01, Math.Cos(u));
        var y = v + inner * Math.Tan(u);
        var o = outer - layout.Corner;
        Point P(double radius, double angle) => At(center, radius, angle);

        return new PetalOutline(P(y, l + R(y)),
        [
            new PetalStep(PetalStepKind.Line, P(o, l + R(o))),
            new PetalStep(PetalStepKind.Quad, P(outer, d + f), Control: P(outer, d)),
            new PetalStep(PetalStepKind.Arc, P(outer, i - f), ArcRadius: outer, IsLargeArc: i - f - d - f > Math.PI, Clockwise: false),
            new PetalStep(PetalStepKind.Quad, P(o, s - R(o)), Control: P(outer, i)),
            new PetalStep(PetalStepKind.Line, P(y, s - R(y))),
            new PetalStep(PetalStepKind.Quad, P(inner, g - u), Control: P(v, s - R(v))),
            new PetalStep(PetalStepKind.Arc, P(inner, c + u), ArcRadius: inner, IsLargeArc: g - u - c - u > Math.PI, Clockwise: true),
            new PetalStep(PetalStepKind.Quad, P(y, l + R(y)), Control: P(v, l + R(v))),
        ]);
    }

    /// <summary>
    /// The petal under <paramref name="p"/>, or -1 in the hole, outside the wheel, in a gap or in a rounded-off corner.
    /// </summary>
    public static int IndexAt(Point center, Point p, PetalLayout layout, int count)
    {
        var (dx, dy) = (p.X - center.X, p.Y - center.Y);
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (count <= 0 || distance < layout.Inner || distance > layout.Outer)
            return -1;

        var angle = Math.Atan2(-dy, dx);
        var span = 2 * Math.PI / count;
        var fromFirst = ((SiteAngle(0, count) - angle) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
        var index = (int)Math.Round(fromFirst / span) % count;
        var offset = Math.IEEERemainder(angle - SiteAngle(index, count), 2 * Math.PI);
        if (Math.Abs(offset) > span / 2 - Math.Asin(layout.Gap / 2 / distance))
            return -1;
        return Contains(Flatten(center, Petal(center, layout, index, count)), p) ? index : -1;
    }

    /// <summary>The site's angle of petal <paramref name="index"/>: counter-clockwise from 3 o'clock.</summary>
    private static double SiteAngle(int index, int count) =>
        (count == 3 ? Math.PI : Math.PI / 2) - 2 * Math.PI * index / count;

    private static Point At(Point center, double radius, double siteAngle) =>
        new(center.X + Math.Cos(siteAngle) * radius, center.Y - Math.Sin(siteAngle) * radius);

    /// <summary>The outline as a polygon: curves and arcs cut into short lines.</summary>
    private static List<Point> Flatten(Point center, PetalOutline outline)
    {
        const int pieces = 12;
        var points = new List<Point> { outline.Start };
        var from = outline.Start;
        foreach (var step in outline.Steps)
        {
            switch (step.Kind)
            {
                case PetalStepKind.Quad:
                    for (var k = 1; k <= pieces; k++)
                    {
                        var t = (double)k / pieces;
                        var (a, b, c) = ((1 - t) * (1 - t), 2 * (1 - t) * t, t * t);
                        points.Add(new Point(a * from.X + b * step.Control.X + c * step.To.X, a * from.Y + b * step.Control.Y + c * step.To.Y));
                    }
                    break;
                case PetalStepKind.Arc:
                    // Clockwise on screen is the growing clockwise-from-top angle.
                    var start = DonutGeometry.AngleOf(center, from);
                    var sweep = DonutGeometry.AngleOf(center, step.To) - start;
                    sweep = step.Clockwise ? (sweep % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI)
                        : -((-sweep % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI));
                    var arcPieces = Math.Max(pieces, (int)Math.Ceiling(Math.Abs(sweep) * 24));
                    for (var k = 1; k <= arcPieces; k++)
                        points.Add(DonutGeometry.PointAt(center, step.ArcRadius, start + sweep * k / arcPieces));
                    break;
                default:
                    points.Add(step.To);
                    break;
            }
            from = step.To;
        }
        return points;
    }

    /// <summary>Even-odd test of <paramref name="p"/> against a closed polygon.</summary>
    private static bool Contains(List<Point> polygon, Point p)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}
