// fork: what an editor window asked for (ImageOptions.Edit, Phase 5), done on the upright picture before any other
// option: Edit's sliders, Redact's boxes (they are placed on the whole picture), Annotate's marks, then Crop. A blurred box is made coarse before it
// is blurred, so no pass of sharpening brings the text back.

using Filee.Core.Editing;
using ImageMagick;
using ImageMagick.Drawing;

namespace Filee.Engines.Magick;

internal static class ImageEdits
{
    /// <summary>Applies <paramref name="edit"/> to <paramref name="image"/>, already turned upright (nothing for null).</summary>
    public static void Apply(IMagickImage<byte> image, ImageEdit? edit)
    {
        if (edit is null)
            return;
        var (width, height) = ((int)image.Width, (int)image.Height);
        if (edit.Adjust is { IsNone: false } adjust)
            Adjust(image, adjust);
        foreach (var box in edit.Redactions)
            Redact(image, CropMath.ToPixels(box, width, height), edit.RedactStyle);
        foreach (var (mark, number) in edit.Annotations)
            Annotate(image, mark, number, width, height);
        if (edit.Redactions.Count > 0 || edit.Annotations.Count > 0 || edit.Crop is not null || edit.Adjust is { IsNone: false })
            DropThumbnail(image); // the small EXIF preview would still show the picture as it was
        if (edit.Crop is not { } crop)
            return;
        var pixels = CropMath.ToPixels(crop, width, height);
        image.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
        image.ResetPage();
    }

    /// <summary>
    /// Edit's sliders, in this order: exposure (a multiply in stops), contrast (an S-curve around the middle grey, or its
    /// inverse), saturation, warmth (red up and blue down, or the other way), sharpness (an unsharp mask). The Edit
    /// window's preview runs this on its small copy.
    /// </summary>
    internal static void Adjust(IMagickImage<byte> image, Adjustments adjust)
    {
        var a = adjust.Clamped();
        if (a.Exposure != 0)
            image.Evaluate(Channels.RGB, EvaluateOperator.Multiply, a.ExposureFactor);
        if (a.Contrast > 0)
            image.SigmoidalContrast(a.Contrast / 100.0 * 8, new Percentage(50));
        else if (a.Contrast < 0)
            image.InverseSigmoidalContrast(-a.Contrast / 100.0 * 8, new Percentage(50));
        if (a.Saturation != 0)
            image.Modulate(new Percentage(100), new Percentage(100 + a.Saturation), new Percentage(100));
        if (a.Warmth != 0)
        {
            image.Evaluate(Channels.Red, EvaluateOperator.Multiply, 1 + a.Warmth / 500.0);
            image.Evaluate(Channels.Blue, EvaluateOperator.Multiply, 1 - a.Warmth / 500.0);
        }
        if (a.Sharpness > 0)
            image.UnsharpMask(0, 1.2, a.Sharpness / 100.0 * 2, 0.02);
    }

    private static void Redact(IMagickImage<byte> image, PixelRect box, RedactStyle style)
    {
        if (style == RedactStyle.Solid)
        {
            new Drawables().FillColor(MagickColors.Black).StrokeColor(MagickColors.Transparent)
                .Rectangle(box.X, box.Y, box.X + box.Width - 1, box.Y + box.Height - 1).Draw(image);
            return;
        }
        using var part = image.CloneArea(box.X, box.Y, (uint)box.Width, (uint)box.Height);
        part.Scale((uint)Math.Max(1, box.Width / 12), (uint)Math.Max(1, box.Height / 12));
        part.Scale((uint)box.Width, (uint)box.Height);
        part.GaussianBlur(0, Math.Max(2, Math.Min(box.Width, box.Height) / 10.0));
        image.Composite(part, box.X, box.Y, CompositeOperator.Copy);
    }

    /// <summary>Draws one Annotate mark at the sizes the editor window shows (AnnotationGeometry).</summary>
    private static void Annotate(IMagickImage<byte> image, Annotation mark, int number, int width, int height)
    {
        var side = Math.Min(width, height);
        var color = new MagickColor(mark.Color);
        var stroke = Math.Max(2, side * AnnotationGeometry.Stroke);
        var (x1, y1, x2, y2) = (mark.X1 * width, mark.Y1 * height, mark.X2 * width, mark.Y2 * height);
        var draw = new Drawables();
        switch (mark.Kind)
        {
            case AnnotationKind.Arrow:
                var (left, right) = AnnotationGeometry.ArrowHead(x1, y1, x2, y2, AnnotationGeometry.HeadSize(side));
                draw.StrokeColor(color).StrokeWidth(stroke).StrokeLineCap(LineCap.Round).FillColor(MagickColors.Transparent)
                    .Line(x1, y1, (left.X + right.X) / 2, (left.Y + right.Y) / 2)
                    .FillColor(color).StrokeWidth(1)
                    .Polygon(new PointD(x2, y2), new PointD(left.X, left.Y), new PointD(right.X, right.Y));
                break;
            case AnnotationKind.Rectangle:
                draw.StrokeColor(color).StrokeWidth(stroke).FillColor(MagickColors.Transparent)
                    .RoundRectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2), stroke * 1.5, stroke * 1.5);
                break;
            case AnnotationKind.Ellipse:
                draw.StrokeColor(color).StrokeWidth(stroke).FillColor(MagickColors.Transparent)
                    .Ellipse((x1 + x2) / 2, (y1 + y2) / 2, Math.Abs(x2 - x1) / 2, Math.Abs(y2 - y1) / 2, 0, 360);
                break;
            case AnnotationKind.Text when mark.Text.Length > 0:
                var size = side * AnnotationGeometry.TextSize;
                draw.Font("Arial").FontPointSize(size).FillColor(color).StrokeColor(MagickColors.Transparent)
                    .TextAlignment(TextAlignment.Left).Text(x1, y1 + size * 0.85, mark.Text);
                break;
            case AnnotationKind.Callout:
                var radius = side * AnnotationGeometry.CalloutRadius;
                draw.FillColor(color).StrokeColor(MagickColors.White).StrokeWidth(Math.Max(1.5, stroke * 0.6))
                    .Circle(x1, y1, x1 + radius, y1)
                    .Font("Arial").FontPointSize(radius * 1.15).FillColor(MagickColors.White).StrokeColor(MagickColors.Transparent)
                    .TextAlignment(TextAlignment.Center).Text(x1, y1 + radius * 0.4, number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                return;
        }
        draw.Draw(image);
    }

    private static void DropThumbnail(IMagickImage<byte> image)
    {
        if (image.GetExifProfile() is { } exif && exif.ThumbnailLength > 0)
        {
            exif.RemoveThumbnail();
            image.SetProfile(exif);
        }
    }
}
