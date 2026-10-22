// fork: the Tools wheel's Add BG (ImageOptions.Backdrop): the picture with rounded corners on a white, black or
// orange background, about 8 % of its longer side around it. Tangerine's window offers more (an image, the aspect
// ratio, the spacing); these are the fixed choices of its popover until Phase 5.

using Filee.Core.Presets;
using ImageMagick;
using ImageMagick.Drawing;

namespace Filee.Engines.Magick;

internal static class ImageBackdrops
{
    /// <summary>Padding on every side, as a share of the picture's longer side.</summary>
    internal const double Padding = 0.08;

    /// <summary>Corner radius, as a share of the picture's shorter side.</summary>
    internal const double CornerRadius = 0.04;

    /// <summary>Puts <paramref name="image"/> on <paramref name="backdrop"/> (nothing for <see cref="ImageBackdrop.None"/>).</summary>
    public static void Apply(IMagickImage<byte> image, ImageBackdrop backdrop)
    {
        if (backdrop == ImageBackdrop.None)
            return;
        var (width, height) = (image.Width, image.Height);
        var radius = Math.Max(1, Math.Min(width, height) * CornerRadius);

        using (var mask = new MagickImage(MagickColors.Transparent, width, height))
        {
            new Drawables().FillColor(MagickColors.White).RoundRectangle(0, 0, width - 1, height - 1, radius, radius).Draw(mask);
            image.Alpha(AlphaOption.Set);
            image.Composite(mask, CompositeOperator.DstIn, Channels.Alpha);
        }

        var pad = (uint)Math.Max(1, Math.Round(Math.Max(width, height) * Padding));
        image.BackgroundColor = MagickColors.Transparent;
        image.Extent(width + 2 * pad, height + 2 * pad, Gravity.Center);
        using var background = Background(backdrop, image.Width, image.Height);
        image.Composite(background, CompositeOperator.DstOver);
    }

    private static MagickImage Background(ImageBackdrop backdrop, uint width, uint height) => backdrop switch
    {
        ImageBackdrop.Black => new MagickImage(MagickColors.Black, width, height),
        // Flick's orange, lighter at the top.
        ImageBackdrop.OrangeGradient => new MagickImage("gradient:#FF8A3D-#FF5200", new MagickReadSettings { Width = width, Height = height }),
        _ => new MagickImage(MagickColors.White, width, height),
    };
}
