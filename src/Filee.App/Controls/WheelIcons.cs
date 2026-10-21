// fork: which line icon (Styles/WheelIcons.axaml) a wheel slice shows. Tangerine's tool ids
// (docs/TANGERINE-SPEC.md), mapped from the SF Symbol it uses to the closest Lucide icon, and the built-in presets that
// do the same job. Any other slice shows its name only.

using Avalonia;
using Avalonia.Media;

namespace Filee.App.Controls;

public static class WheelIcons
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        // Tools
        ["compress"] = "minimize-2",
        ["removeMetadata"] = "tag",
        ["editImage"] = "sliders-horizontal",
        ["annotateImage"] = "pencil-line",
        ["frameImage"] = "image",
        ["cropImage"] = "crop",
        ["cropVideo"] = "crop",
        ["redactImage"] = "eye-off",
        ["redactVideo"] = "eye-off",
        ["redactAudio"] = "mic-off",
        ["normalizeAudio"] = "audio-waveform",
        ["audioToVideo"] = "audio-lines",
        ["trimVideo"] = "scissors",
        ["trimAudio"] = "scissors",
        ["audioChannels"] = "speaker",
        ["muteVideo"] = "volume-x",
        ["changeVideoSpeed"] = "gauge",
        ["videoSnapshots"] = "camera",
        ["splitVideo"] = "film",
        ["joinVideos"] = "layers",
        ["splitPDF"] = "columns-2",
        ["mergePDF"] = "files",
        ["organizePDF"] = "layout-grid",
        ["createPDF"] = "file-plus",
        ["createCollage"] = "layout-dashboard",
        ["extractArchive"] = "archive",
        // Built-in presets doing one of those jobs
        ["compress-pdf"] = "minimize-2",
        ["tool-crop"] = "crop",
        ["pdf-split"] = "columns-2",
        ["merge-pdf"] = "files",
        ["extract"] = "archive",
        ["extract-frames-png"] = "camera",
        ["extract-frames-jpg"] = "camera",
    };

    /// <summary>The Lucide icon name for a tool or preset id, or null.</summary>
    public static string? GlyphFor(string id) => Glyphs.GetValueOrDefault(id);

    /// <summary>The icon's geometry (24 × 24, drawn as a stroke), or null when the id has none.</summary>
    public static Geometry? Find(string id) =>
        GlyphFor(id) is { } glyph && Application.Current?.TryGetResource("WheelIcon." + glyph, null, out var value) == true
            ? value as Geometry
            : null;
}
