// fork: what an editor window asked for (ImageOptions.Edit, Phase 5), done on the upright picture before any other
// option: Redact's boxes first (they are placed on the whole picture), then Crop. A blurred box is made coarse before it
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
        foreach (var box in edit.Redactions)
            Redact(image, CropMath.ToPixels(box, width, height), edit.RedactStyle);
        if (edit.Redactions.Count > 0 || edit.Crop is not null)
            DropThumbnail(image); // the small EXIF preview would still show the picture as it was
        if (edit.Crop is not { } crop)
            return;
        var pixels = CropMath.ToPixels(crop, width, height);
        image.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
        image.ResetPage();
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

    private static void DropThumbnail(IMagickImage<byte> image)
    {
        if (image.GetExifProfile() is { } exif && exif.ThumbnailLength > 0)
        {
            exif.RemoveThumbnail();
            image.SetProfile(exif);
        }
    }
}
