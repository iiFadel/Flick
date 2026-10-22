// fork: the Tools wheel's operations: a tool run on files of one format, written as a new file of the same format next
// to them (docs/TANGERINE-SPEC.md, "Tools wheel"). Each is a Preset built here, never saved: TargetFormat "*" (the
// source's own format) plus the options that do the work, so the job, route and output pipeline stay the conversions'.
// Pure, unit tested (ToolOperationsTests).

using Filee.Core.Formats;
using Filee.Core.Presets;

namespace Filee.Core.Wheels;

public static class ToolOperations
{
    private static readonly HashSet<string> Images = ["jpg", "png", "webp", "heic", "tiff", "gif"];
    private static readonly HashSet<string> Audio = ["mp3", "m4a", "wav", "flac"];
    private static readonly HashSet<string> Video = ["mp4", "mov", "mkv"];
    private static readonly HashSet<string> Archives = ["zip", "tar", "tgz", "rar"];

    /// <summary>
    /// The preset that runs <paramref name="tool"/> on files of <paramref name="formats"/> (FormatRegistry ids, one
    /// per file), or null when Flick can't run it on them yet.
    /// </summary>
    /// <param name="suffix">The words a tool adds after the file name ("Compressed"), by tool id.</param>
    public static Preset? For(string tool, IReadOnlyList<string> formats, Func<string, string> suffix)
    {
        if (formats.Count == 0)
            return null;
        var preset = tool switch
        {
            "compress" => Compress(formats),
            "removeMetadata" => Metadata(formats),
            "muteVideo" => formats.All(Video.Contains) ? Mute() : null,
            "normalizeAudio" => formats.All(Audio.Contains) ? Normalize() : null,
            "audioChannels" => formats.All(Audio.Contains) ? SameFormat() : null,
            "extractArchive" => formats.All(Archives.Contains) ? Extract() : null,
            _ => null,
        };
        if (preset is null)
            return null;
        preset.Id = "op-" + tool;
        preset.Output.FileNamePattern = "{name} " + suffix(tool);
        preset.Output.PlainCopyNumbers = true;
        return preset;
    }

    /// <summary>
    /// The value <paramref name="tool"/> asks for in the popover after the drop, or null for a tool that runs at once.
    /// Only meaningful where <see cref="For"/> gives an operation.
    /// </summary>
    /// <param name="text">Flick's text for a key (<c>tool.choice.mono</c>).</param>
    public static ToolParameter? ParameterFor(string tool, Func<string, string> text) => tool switch
    {
        "audioChannels" => new ToolParameter(
        [
            new ToolChoice(text("tool.choice.mono"), text("tool.choice.mono"), p => p.Media.AudioChannels = 1),
            new ToolChoice(text("tool.choice.stereo"), text("tool.choice.stereo"), p => p.Media.AudioChannels = 2),
        ]),
        _ => null,
    };

    /// <summary>
    /// Fixed defaults until Tangerine's Compress window (Phase 5): photos at quality 70, PNGs to a 256-colour palette,
    /// TIFFs with ZIP, videos as small H.264, songs at 128 kbit/s (WAV becomes FLAC, FLAC its strongest setting),
    /// PDFs as Compress PDF does. Never larger than the source (<see cref="OutputRule.NeverLarger"/>).
    /// </summary>
    private static Preset? Compress(IReadOnlyList<string> formats)
    {
        var preset = SameFormat();
        preset.Output.NeverLarger = true;
        if (formats.All(f => f is "jpg" or "webp" or "heic"))
            preset.Image.Quality = 70;
        else if (formats.All(f => f == "png"))
            preset.Image.ReduceColors = true;
        else if (formats.All(f => f == "tiff"))
            preset.Image.TiffCompression = TiffCompression.Zip;
        else if (formats.All(f => f is "jpg" or "webp" or "heic" or "png" or "tiff"))
        {
            // A mixed selection gets every format's own setting.
            preset.Image.Quality = 70;
            preset.Image.ReduceColors = true;
            preset.Image.TiffCompression = TiffCompression.Zip;
        }
        else if (formats.All(Video.Contains))
            preset.Media.Quality = MediaQuality.Small;
        else if (formats.All(Audio.Contains))
        {
            preset.Media.Quality = MediaQuality.Small;
            preset.Media.AudioBitrateKbps = 128;
            if (formats.All(f => f == "wav"))
                preset.TargetFormat = "flac";
        }
        else if (formats.All(f => f == "pdf"))
        {
            // The engine keeps the source when nothing got smaller, with its own notice.
            preset.Output.NeverLarger = false;
            preset.Pdf.Compression = PdfCompression.Ebook;
            preset.Pdf.CompressWithGhostscript = true;
        }
        else
            return null;
        return preset;
    }

    private static Preset Normalize()
    {
        var preset = SameFormat();
        preset.Media.NormalizeLoudness = true;
        return preset;
    }

    private static Preset Extract() => new() { TargetFormat = FormatRegistry.Folder, Kind = PresetKind.Convert };

    private static Preset? Metadata(IReadOnlyList<string> formats)
    {
        var preset = SameFormat();
        if (formats.All(Images.Contains))
        {
            preset.Image.OnlyRemoveMetadata = true;
            preset.Image.KeepMetadata = false;
            // A JPEG that can't be rewritten is encoded again at its own quality, never above it (ImageEncoder.ApplyOptions).
            if (formats.All(f => f == "jpg"))
                preset.Image.Quality = 100;
        }
        else if (formats.All(f => Audio.Contains(f) || Video.Contains(f)))
            preset.Media.StripMetadata = true;
        else if (formats.All(f => f == "pdf"))
            preset.Pdf.RemoveMetadata = true;
        else
            return null;
        return preset;
    }

    private static Preset Mute()
    {
        var preset = SameFormat();
        preset.Media.RemoveAudio = true;
        return preset;
    }

    private static Preset SameFormat() => new() { TargetFormat = BuiltInData.SameAsSource, Kind = PresetKind.Convert };
}
