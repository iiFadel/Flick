// fork: the crop editors' geometry (Phase 5): a crop kept as fractions of the picture, so the preview's size never
// matters, and turned into whole pixels only when the file is written. Pure, unit tested (CropMathTests).

namespace Filee.Core.Editing;

/// <summary>A rectangle as fractions (0 to 1) of a picture's width and height, top left at (0, 0).</summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public static readonly NormalizedRect Full = new(0, 0, 1, 1);
}

/// <summary>A rectangle in whole pixels.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public static class CropMath
{
    /// <summary>
    /// <paramref name="rect"/> kept inside the picture, at least one pixel wide and high; a value that isn't a number
    /// counts as 0. A crop is never empty, so no write can fail on it.
    /// </summary>
    public static NormalizedRect Clamp(NormalizedRect rect)
    {
        var x = Math.Clamp(Finite(rect.X), 0, 1);
        var y = Math.Clamp(Finite(rect.Y), 0, 1);
        var width = Math.Clamp(Finite(rect.Width), 0, 1 - x);
        var height = Math.Clamp(Finite(rect.Height), 0, 1 - y);
        return new NormalizedRect(x, y, width, height);
    }

    /// <summary>
    /// <paramref name="rect"/> in pixels of a <paramref name="width"/> × <paramref name="height"/> picture: rounded,
    /// inside it, at least 1 × 1. <paramref name="even"/> rounds the size (and position) down to even numbers, as
    /// H.264 and HEVC need, never below 2 × 2.
    /// </summary>
    public static PixelRect ToPixels(NormalizedRect rect, int width, int height, bool even = false)
    {
        var clamped = Clamp(rect);
        var x = Math.Clamp((int)Math.Round(clamped.X * width), 0, Math.Max(0, width - 1));
        var y = Math.Clamp((int)Math.Round(clamped.Y * height), 0, Math.Max(0, height - 1));
        var w = Math.Clamp((int)Math.Round(clamped.Width * width), 1, Math.Max(1, width - x));
        var h = Math.Clamp((int)Math.Round(clamped.Height * height), 1, Math.Max(1, height - y));
        if (even)
        {
            (x, y) = (x & ~1, y & ~1);
            w = Math.Max(2, Math.Min(w, width - x) & ~1);
            h = Math.Max(2, Math.Min(h, height - y) & ~1);
        }
        return new PixelRect(x, y, w, h);
    }

    /// <summary>
    /// A crop of exactly <paramref name="cropWidth"/> × <paramref name="cropHeight"/> pixels, centred on
    /// <paramref name="around"/>'s centre and moved to stay inside the picture; a size larger than the picture is
    /// cut to it.
    /// </summary>
    public static NormalizedRect Exact(int cropWidth, int cropHeight, int width, int height, NormalizedRect around)
    {
        if (width <= 0 || height <= 0)
            return NormalizedRect.Full;
        var w = Math.Clamp((double)cropWidth / width, 1.0 / width, 1);
        var h = Math.Clamp((double)cropHeight / height, 1.0 / height, 1);
        var centre = Clamp(around);
        var x = Math.Clamp(centre.X + centre.Width / 2 - w / 2, 0, 1 - w);
        var y = Math.Clamp(centre.Y + centre.Height / 2 - h / 2, 0, 1 - h);
        return new NormalizedRect(x, y, w, h);
    }

    /// <summary>
    /// Width over height of an aspect choice in pixels ("1:1", "4:3", "16:9", "9:16", "original"), or 0 for "free" and
    /// anything else.
    /// </summary>
    public static double AspectRatio(string choice, int width, int height) => choice switch
    {
        "1:1" => 1,
        "4:3" => 4.0 / 3,
        "16:9" => 16.0 / 9,
        "9:16" => 9.0 / 16,
        "original" when width > 0 && height > 0 => (double)width / height,
        _ => 0,
    };

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;
}
