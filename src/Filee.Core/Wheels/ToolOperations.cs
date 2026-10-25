// fork: the Tools wheel's operations: a tool run on files of one format, written as a new file of the same format next
// to them (docs/TANGERINE-SPEC.md, "Tools wheel"). Each is a Preset built here, never saved: TargetFormat "*" (the
// source's own format) plus the options that do the work, so the job, route and output pipeline stay the conversions'.
// Pure, unit tested (ToolOperationsTests).

using System.Globalization;
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
            "changeVideoSpeed" => formats.All(Video.Contains) ? SameFormat(p => p.Media.Speed = 2) : null,
            "splitVideo" => formats.All(Video.Contains) ? SameFormat(p => p.Media.SplitParts = 2) : null,
            "videoSnapshots" => formats.All(Video.Contains) ? Snapshot() : null,
            "audioToVideo" => formats.All(Audio.Contains) ? SameFormat(p => p.Media.Visualizer = VisualizerShape.Landscape) : null,
            "frameImage" => formats.All(f => Images.Contains(f) && f != "gif") ? SameFormat(p => p.Image.Backdrop = ImageBackdrop.White) : null,
            "cropImage" => formats.Count == 1 && Images.Contains(formats[0]) && formats[0] != "gif" ? SameFormat() : null,
            "trimVideo" => formats.Count == 1 && Video.Contains(formats[0]) ? SameFormat() : null,
            "trimAudio" => formats.Count == 1 && Audio.Contains(formats[0]) ? SameFormat() : null,
            "cropVideo" => formats.Count == 1 && Video.Contains(formats[0]) ? SameFormat() : null,
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
    /// <param name="text">
    /// Flick's text for a key (<c>tool.choice.mono</c>); <c>tool.choice.parts</c> and <c>tool.choice.frames</c> hold
    /// a <c>{0}</c> for the number.
    /// </param>
    public static ToolParameter? ParameterFor(string tool, Func<string, string> text) => tool switch
    {
        "audioChannels" => new ToolParameter(
        [
            new ToolChoice(text("tool.choice.mono"), text("tool.choice.mono"), p => p.Media.AudioChannels = 1),
            new ToolChoice(text("tool.choice.stereo"), text("tool.choice.stereo"), p => p.Media.AudioChannels = 2),
        ]),
        // Default 2×, the speed Tangerine's window shows first after 1×.
        "changeVideoSpeed" => new ToolParameter(
            [.. new[] { 0.25, 0.5, 1.5, 2, 4 }.Select(speed => new ToolChoice(
                Number(speed) + "×", $"{text("tool.suffix.changeVideoSpeed")} {Number(speed)}x", p => p.Media.Speed = speed))],
            Default: 3),
        "splitVideo" => new ToolParameter(
            [.. new[] { 2, 3, 4 }.Select(parts => new ToolChoice(
                Format(text("tool.choice.parts"), parts), text("tool.suffix.splitVideo"), p => p.Media.SplitParts = parts))]),
        "videoSnapshots" => new ToolParameter(
        [
            new ToolChoice(text("tool.choice.middle_frame"), text("tool.suffix.videoSnapshots"), p => p.Media.Snapshots = 1),
            .. new[] { 5, 10 }.Select(frames => new ToolChoice(
                Format(text("tool.choice.frames"), frames), text("tool.suffix.videoSnapshots_several"), p => p.Media.Snapshots = frames)),
        ]),
        "audioToVideo" => new ToolParameter(
        [
            .. new[] { VisualizerShape.Landscape, VisualizerShape.Portrait, VisualizerShape.Square }.Select(shape => new ToolChoice(
                text("tool.choice." + shape.ToString().ToLowerInvariant()), text("tool.suffix.audioToVideo"), p => p.Media.Visualizer = shape)),
        ]),
        "frameImage" => new ToolParameter(
        [
            new ToolChoice(text("tool.choice.white"), text("tool.suffix.frameImage"), p => p.Image.Backdrop = ImageBackdrop.White),
            new ToolChoice(text("tool.choice.black"), text("tool.suffix.frameImage"), p => p.Image.Backdrop = ImageBackdrop.Black),
            new ToolChoice(text("tool.choice.orange"), text("tool.suffix.frameImage"), p => p.Image.Backdrop = ImageBackdrop.OrangeGradient),
        ]),
        _ => null,
    };

    /// <summary>
    /// The editor window <paramref name="tool"/> opens after the drop (Phase 5), or null for a tool that runs at once
    /// or asks in the popover. Only meaningful where <see cref="For"/> gives an operation; editors take one file.
    /// </summary>
    public static EditorKind? EditorFor(string tool) => tool switch
    {
        "cropImage" => EditorKind.CropImage,
        "trimVideo" or "trimAudio" => EditorKind.Trim,
        "cropVideo" => EditorKind.CropVideo,
        _ => null,
    };

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Format(string text, int number) => string.Format(CultureInfo.InvariantCulture, text, number);

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

    /// <summary>One middle frame as a PNG until the popover picks how many (<see cref="MediaOptions.Snapshots"/>).</summary>
    private static Preset Snapshot()
    {
        var preset = SameFormat(p => p.Media.Snapshots = 1);
        preset.TargetFormat = "png";
        return preset;
    }

    private static Preset SameFormat(Action<Preset> apply)
    {
        var preset = SameFormat();
        apply(preset);
        return preset;
    }

    private static Preset SameFormat() => new() { TargetFormat = BuiltInData.SameAsSource, Kind = PresetKind.Convert };
}
