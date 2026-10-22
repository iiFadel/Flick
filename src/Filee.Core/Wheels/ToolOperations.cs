// fork: the Tools wheel's operations: a tool run on files of one format, written as a new file of the same format next
// to them (docs/TANGERINE-SPEC.md, "Tools wheel"). Each is a Preset built here, never saved: TargetFormat "*" (the
// source's own format) plus the options that do the work, so the job, route and output pipeline stay the conversions'.
// Pure, unit tested (ToolOperationsTests).

using Filee.Core.Presets;

namespace Filee.Core.Wheels;

public static class ToolOperations
{
    private static readonly HashSet<string> Images = ["jpg", "png", "webp", "heic", "tiff", "gif"];
    private static readonly HashSet<string> Audio = ["mp3", "m4a", "wav", "flac"];
    private static readonly HashSet<string> Video = ["mp4", "mov", "mkv"];

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
            "removeMetadata" => Metadata(formats),
            "muteVideo" => formats.All(Video.Contains) ? Mute() : null,
            _ => null,
        };
        if (preset is null)
            return null;
        preset.Id = "op-" + tool;
        preset.Output.FileNamePattern = "{name} " + suffix(tool);
        preset.Output.PlainCopyNumbers = true;
        return preset;
    }

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
