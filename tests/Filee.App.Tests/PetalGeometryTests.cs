// fork: Tangerine's wheel geometry (PetalGeometry), checked against the paths its website draws.

using System.Globalization;
using Avalonia;
using Filee.App.Controls;

namespace Filee.App.Tests;

public class PetalGeometryTests
{
    private static readonly Point Origin = new(0, 0);
    private static readonly PetalLayout Site = PetalLayout.ForOuter(PetalLayout.SiteOuter);

    // Copied from tangerineformac.com (the preview wheels: 7 image formats, 6 file actions).
    private const string SevenFirst = "M 22.6358 -51.6132 L 41.3000 -90.3699 Q 50.0486 -108.5365 33.2671 -114.7969 A 119.52000000000001 119.52000000000001 0 0 0 -33.2671 -114.7969 Q -50.0486 -108.5365 -41.3000 -90.3699 L -22.6358 -51.6132 Q -19.5138 -45.1305 -12.5471 -46.9938 A 48.64000000000001 48.64000000000001 0 0 1 12.5471 -46.9938 Q 19.5138 -45.1305 22.6358 -51.6132 Z";
    private const string SevenLast = "M -26.2397 -49.8777 L -44.9039 -88.6344 Q -53.6525 -106.8010 -69.0101 -97.5840 A 119.52000000000001 119.52000000000001 0 0 0 -110.4935 -45.5654 Q -116.0620 -28.5418 -96.4042 -24.0551 L -54.4660 -14.4829 Q -47.4511 -12.8818 -44.5642 -19.4905 A 48.64000000000001 48.64000000000001 0 0 1 -28.9183 -39.1099 Q -23.1177 -43.3949 -26.2397 -49.8777 Z";
    private const string SixFirst = "M 27.2093 -51.1279 L 47.9379 -87.0309 Q 58.0196 -104.4929 41.7529 -111.9899 A 119.52000000000001 119.52000000000001 0 0 0 -41.7529 -111.9899 Q -58.0196 -104.4929 -47.9379 -87.0309 L -27.2093 -51.1279 Q -22.9392 -43.7319 -14.7815 -46.3396 A 48.64000000000001 48.64000000000001 0 0 1 14.7815 -46.3396 Q 22.9392 -43.7319 27.2093 -51.1279 Z";
    private const string SixLast = "M -30.6734 -49.1279 L -51.4020 -85.0309 Q -61.4837 -102.4929 -76.1096 -92.1540 A 119.52000000000001 119.52000000000001 0 0 0 -117.8625 -19.8359 Q -119.5033 -2.0000 -99.3399 -2.0000 L -57.8827 -2.0000 Q -49.3425 -2.0000 -47.5220 -10.3686 A 48.64000000000001 48.64000000000001 0 0 1 -32.7405 -35.9710 Q -26.4033 -41.7319 -30.6734 -49.1279 Z";

    [Theory]
    [InlineData(SevenFirst, 0, 7)]
    [InlineData(SevenLast, 6, 7)]
    [InlineData(SixFirst, 0, 6)]
    [InlineData(SixLast, 5, 6)]
    public void A_petal_is_the_one_the_site_draws(string svg, int index, int count)
    {
        var expected = Parse(svg);
        var outline = PetalGeometry.Petal(Origin, Site, index, count);

        AssertNear(expected.Start, outline.Start);
        Assert.Equal(expected.Steps.Count, outline.Steps.Count);
        for (var i = 0; i < expected.Steps.Count; i++)
        {
            var (want, got) = (expected.Steps[i], outline.Steps[i]);
            Assert.Equal(want.Kind, got.Kind);
            AssertNear(want.To, got.To);
            if (want.Kind == PetalStepKind.Quad)
                AssertNear(want.Control, got.Control);
            if (want.Kind == PetalStepKind.Arc)
            {
                Assert.Equal(want.ArcRadius, got.ArcRadius, 3);
                Assert.Equal(want.IsLargeArc, got.IsLargeArc);
                Assert.Equal(want.Clockwise, got.Clockwise);
            }
        }
    }

    [Fact]
    public void The_layout_is_the_sites_and_scales_with_the_outer_radius()
    {
        Assert.Equal(48.64, Site.Inner, 6);
        Assert.Equal(119.52, Site.Outer, 6);
        Assert.Equal(125.92, Site.Backdrop, 6);
        Assert.Equal(42.24, Site.Hole, 6);
        Assert.Equal(4, Site.Gap, 6);
        Assert.Equal(20.16, Site.Corner, 6);
        Assert.Equal(84.5312, Site.LabelRadius, 6);

        var twice = PetalLayout.ForOuter(2 * PetalLayout.SiteOuter);
        Assert.Equal(2 * Site.Backdrop, twice.Backdrop, 6);
        Assert.Equal(2 * Site.Gap, twice.Gap, 6);
        var small = PetalGeometry.Petal(Origin, Site, 2, 6);
        var big = PetalGeometry.Petal(Origin, twice, 2, 6);
        AssertNear(new Point(2 * small.Start.X, 2 * small.Start.Y), big.Start);
    }

    [Fact]
    public void Slices_go_clockwise_from_the_top_except_three_which_start_on_the_left()
    {
        Assert.Equal(0, PetalGeometry.MidAngle(0, 1), 9);
        Assert.Equal(0, PetalGeometry.MidAngle(0, 6), 9);
        Assert.Equal(Math.PI / 3, PetalGeometry.MidAngle(1, 6), 9);
        Assert.Equal(3 * Math.PI / 2, PetalGeometry.MidAngle(0, 3), 9);
        Assert.Equal(Math.PI / 6, PetalGeometry.MidAngle(1, 3), 9);
        Assert.Equal(5 * Math.PI / 6, PetalGeometry.MidAngle(2, 3), 9);

        AssertNear(new Point(0, -84.5312), PetalGeometry.LabelPoint(Origin, Site, 0, 6));
        AssertNear(new Point(-84.5312, 0), PetalGeometry.LabelPoint(Origin, Site, 0, 3));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(12)]
    public void The_donuts_angles_agree_with_the_petals(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(PetalGeometry.MidAngle(i, count), DonutGeometry.MidAngle(i, count), 9);
            Assert.Equal(i, DonutGeometry.IndexAt(PetalGeometry.MidAngle(i, count), count));
            var label = PetalGeometry.LabelPoint(Origin, Site, i, count);
            Assert.Equal(i, PetalGeometry.IndexAt(Origin, label, Site, count));
            Assert.Equal(i, DonutGeometry.HitTest(Origin, label, Site.Inner, Site.Outer, count).Index);
        }
    }

    [Fact]
    public void A_petal_by_angle_is_the_petal_by_index()
    {
        var byIndex = PetalGeometry.Petal(Origin, Site, 1, 3);
        var byAngle = PetalGeometry.Petal(Origin, Site, DonutGeometry.MidAngle(1, 3), DonutGeometry.Span(3));
        AssertNear(byIndex.Start, byAngle.Start);
        for (var i = 0; i < byIndex.Steps.Count; i++)
            AssertNear(byIndex.Steps[i].To, byAngle.Steps[i].To);
    }

    [Fact]
    public void One_slice_is_a_ring_with_its_gap_at_the_bottom()
    {
        Assert.Equal(0, Hit(0, -84, 1));
        Assert.Equal(0, Hit(84, 0, 1));
        Assert.Equal(-1, Hit(0, 84, 1));
        Assert.Equal(0, Hit(5, 84, 1));
    }

    [Fact]
    public void Two_slices_are_the_top_and_bottom_halves()
    {
        Assert.Equal(0, Hit(0, -84, 2));
        Assert.Equal(1, Hit(0, 84, 2));
        Assert.Equal(-1, Hit(84, 0, 2));
        Assert.Equal(-1, Hit(-84, 0, 2));
    }

    [Fact]
    public void Three_slices_put_the_first_on_the_left()
    {
        Assert.Equal(0, Hit(-84, 0, 3));
        Assert.Equal(1, Hit(84 * Math.Cos(Math.PI / 6), -42, 3));
        Assert.Equal(2, Hit(84 * Math.Cos(Math.PI / 6), 42, 3));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(84)]
    [InlineData(95)]
    public void The_gap_is_four_pixels_wide_along_the_straight_sides(double radius)
    {
        // The gap between slices 0 and 1 of six runs along 30° clockwise from the top.
        var along = new Vector(Math.Sin(Math.PI / 6), -Math.Cos(Math.PI / 6));
        var across = new Vector(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)); // towards slice 1
        var onLine = along * radius;

        Assert.Equal(-1, Hit(onLine.X, onLine.Y, 6));
        Assert.Equal(-1, Hit((onLine + across * 1.9).X, (onLine + across * 1.9).Y, 6));
        Assert.Equal(-1, Hit((onLine - across * 1.9).X, (onLine - across * 1.9).Y, 6));
        Assert.Equal(1, Hit((onLine + across * 2.5).X, (onLine + across * 2.5).Y, 6));
        Assert.Equal(0, Hit((onLine - across * 2.5).X, (onLine - across * 2.5).Y, 6));
    }

    [Fact]
    public void The_straight_sides_stay_half_a_gap_from_the_gap_line()
    {
        var outline = PetalGeometry.Petal(Origin, Site, 0, 6);
        // M and the first L are on the side next to slice 1 (30°); the second L's ends are on the side next to slice 5.
        Assert.Equal(2, DistanceToLine(outline.Start, Math.PI / 6), 6);
        Assert.Equal(2, DistanceToLine(outline.Steps[0].To, Math.PI / 6), 6);
        Assert.Equal(2, DistanceToLine(outline.Steps[3].To, -Math.PI / 6), 6);
        Assert.Equal(2, DistanceToLine(outline.Steps[4].To, -Math.PI / 6), 6);
    }

    [Fact]
    public void The_hole_the_outside_and_the_rounded_corners_select_nothing()
    {
        Assert.Equal(-1, Hit(0, 0, 6));
        Assert.Equal(-1, Hit(0, -45, 6));
        Assert.Equal(-1, Hit(0, -122, 6));
        Assert.Equal(-1, Hit(0, -200, 6));

        // Just inside slice 0's side, near the outer circle: cut away by the rounded corner. Further in: the slice.
        var side = Math.PI / 6;
        Point At(double r) => DonutGeometry.PointAt(Origin, r, side - Math.Asin(2.4 / r));
        Assert.Equal(-1, Hit(At(118.5).X, At(118.5).Y, 6));
        Assert.Equal(0, Hit(At(90).X, At(90).Y, 6));
    }

    [Fact]
    public void Hits_follow_the_centre()
    {
        var c = new Point(136, 136);
        Assert.Equal(0, PetalGeometry.IndexAt(c, new Point(136, 136 - 84), Site, 6));
        Assert.Equal(-1, PetalGeometry.IndexAt(c, c, Site, 6));
        Assert.Equal(-1, PetalGeometry.IndexAt(c, new Point(136, 52), Site, 0));
    }

    private static int Hit(double x, double y, int count) => PetalGeometry.IndexAt(Origin, new Point(x, y), Site, count);

    /// <summary>Distance of <paramref name="p"/> from the line through the origin at <paramref name="angle"/> (clockwise from top).</summary>
    private static double DistanceToLine(Point p, double angle) =>
        Math.Abs(p.X * -Math.Cos(angle) - p.Y * Math.Sin(angle));

    /// <summary>Reads the M / L / Q / A / Z paths the site writes.</summary>
    private static PetalOutline Parse(string svg)
    {
        var t = svg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double N(int i) => double.Parse(t[i], CultureInfo.InvariantCulture);
        var start = new Point(N(1), N(2));
        var steps = new List<PetalStep>();
        for (var i = 3; i < t.Length;)
        {
            switch (t[i])
            {
                case "L":
                    steps.Add(new PetalStep(PetalStepKind.Line, new Point(N(i + 1), N(i + 2))));
                    i += 3;
                    break;
                case "Q":
                    steps.Add(new PetalStep(PetalStepKind.Quad, new Point(N(i + 3), N(i + 4)), Control: new Point(N(i + 1), N(i + 2))));
                    i += 5;
                    break;
                case "A":
                    steps.Add(new PetalStep(PetalStepKind.Arc, new Point(N(i + 6), N(i + 7)), ArcRadius: N(i + 1),
                        IsLargeArc: t[i + 4] == "1", Clockwise: t[i + 5] == "1"));
                    i += 8;
                    break;
                default:
                    i++;
                    break;
            }
        }
        return new PetalOutline(start, steps);
    }

    private static void AssertNear(Point expected, Point actual) =>
        Assert.True(Math.Abs(expected.X - actual.X) <= 1e-3 && Math.Abs(expected.Y - actual.Y) <= 1e-3,
            $"expected {expected}, got {actual}");
}
