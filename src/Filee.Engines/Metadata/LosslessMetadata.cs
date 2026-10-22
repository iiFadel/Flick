// fork: the Tools wheel's Metadata for pictures (ImageOptions.OnlyRemoveMetadata): the file is rewritten without its
// metadata blocks and the picture is never decoded, so not one pixel changes. The engines that own the pictures'
// same-format routes (Magick, and FFmpeg for HEIC) ask here first and convert as usual when this can't do the file.

using Filee.Core.Conversion;

namespace Filee.Engines.Metadata;

internal static class LosslessMetadata
{
    private static readonly Dictionary<string, Func<byte[], byte[]?>> Strippers = new(StringComparer.Ordinal)
    {
        ["jpg"] = JpegMetadata.Strip,
        ["png"] = PngMetadata.Strip,
        ["webp"] = WebpMetadata.Strip,
        ["gif"] = GifMetadata.Strip,
        ["tiff"] = TiffMetadata.Strip,
        ["heic"] = HeifMetadata.Strip,
        ["heif"] = HeifMetadata.Strip,
        ["avif"] = HeifMetadata.Strip,
    };

    /// <summary>True when <paramref name="step"/> only removes the metadata of a format this rewrites directly.</summary>
    public static bool Applies(ConversionStep step) =>
        step.Preset.Image.OnlyRemoveMetadata && step.From == step.To && Strippers.ContainsKey(step.From) && !step.FrameSequence && !step.MotionPhoto;

    /// <returns>The file written, or null when this can't read the file (the engine then converts it as usual).</returns>
    public static async Task<IReadOnlyList<string>?> TryRunAsync(ConversionStep step, CancellationToken cancellationToken)
    {
        if (!Applies(step))
            return null;
        var stripped = Strippers[step.From](await File.ReadAllBytesAsync(step.InputPath, cancellationToken));
        if (stripped is null)
            return null;
        var output = step.Output.Allocate(Path.GetExtension(step.InputPath).TrimStart('.'));
        if (output is null)
            return [];
        await File.WriteAllBytesAsync(output, stripped, cancellationToken);
        return [output];
    }
}
