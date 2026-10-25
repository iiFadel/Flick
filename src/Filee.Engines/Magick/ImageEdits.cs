// fork: what an editor window asked for (ImageOptions.Edit, Phase 5), done on the upright picture before any other
// option.

using Filee.Core.Editing;
using ImageMagick;

namespace Filee.Engines.Magick;

internal static class ImageEdits
{
    /// <summary>Applies <paramref name="edit"/> to <paramref name="image"/>, already turned upright (nothing for null).</summary>
    public static void Apply(IMagickImage<byte> image, ImageEdit? edit)
    {
        if (edit?.Crop is not { } crop)
            return;
        var pixels = CropMath.ToPixels(crop, (int)image.Width, (int)image.Height);
        image.Crop(new MagickGeometry(pixels.X, pixels.Y, (uint)pixels.Width, (uint)pixels.Height));
        image.ResetPage();
        // The small EXIF preview would still show the whole picture.
        if (image.GetExifProfile() is { } exif && exif.ThumbnailLength > 0)
        {
            exif.RemoveThumbnail();
            image.SetProfile(exif);
        }
    }
}
