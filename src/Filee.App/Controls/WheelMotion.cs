// fork: Tangerine's wheel timing (docs/TANGERINE-SPEC.md, "Motion"). Pure, unit tested (WheelMotionTests).

namespace Filee.App.Controls;

public static class WheelMotion
{
    /// <summary>The wheel blooms from 45 % to full size and from clear to opaque in this long (site: 200 ms).</summary>
    public const double OpenSeconds = 0.2;

    /// <summary>The labels start fading in this long after the bloom starts (recording: about 40 ms) …</summary>
    public const double LabelDelaySeconds = 0.04;

    /// <summary>… and take this long to appear.</summary>
    public const double LabelFadeSeconds = 0.08;

    /// <summary>A petal's fill changes to and from the accent in this long (site: transition fill .12s).</summary>
    public const double HoverSeconds = 0.12;

    /// <summary>How far the bloom is (0 to 1) at <paramref name="t"/> (0 to 1) of <see cref="OpenSeconds"/>: the site's cubic-bezier(.18, .82, .24, 1).</summary>
    public static double Open(double t) => CubicBezier(Math.Clamp(t, 0, 1), 0.18, 0.82, 0.24, 1);

    /// <summary>How visible the labels are <paramref name="seconds"/> after the bloom starts.</summary>
    public static double LabelAlpha(double seconds) => Math.Clamp((seconds - LabelDelaySeconds) / LabelFadeSeconds, 0, 1);

    /// <summary>
    /// A CSS cubic-bezier timing function: the curve from (0, 0) to (1, 1) through the control points
    /// (<paramref name="x1"/>, <paramref name="y1"/>) and (<paramref name="x2"/>, <paramref name="y2"/>), read at
    /// <paramref name="x"/>.
    /// </summary>
    public static double CubicBezier(double x, double x1, double y1, double x2, double y2)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        // Find the curve's parameter for x (x grows with it on a timing curve), then read y there.
        double low = 0, high = 1, t = x;
        for (var i = 0; i < 40; i++)
        {
            t = (low + high) / 2;
            if (Bezier(t, x1, x2) < x)
                low = t;
            else
                high = t;
        }
        return Bezier(t, y1, y2);
    }

    private static double Bezier(double t, double p1, double p2) =>
        3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t * t * p2 + t * t * t;
}
