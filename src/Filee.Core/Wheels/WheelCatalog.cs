// fork: what Flick's two wheels hold (docs/TANGERINE-SPEC.md, "Wheel contents"), ported from Tangerine's website demo:
// z() for the Convert wheel, et() and its batch filter for the Tools wheel. Formats are FormatRegistry ids; the
// site's "gz" is a gzipped tarball (FormatRegistry "tgz"). Pure, unit tested (WheelCatalogTests).

namespace Filee.Core.Wheels;

public static class WheelCatalog
{
    private static readonly string[] ImageSources = ["jpg", "png", "webp", "heic", "tiff", "svg", "avif", "bmp"];
    private static readonly string[] ImageTargets = ["jpg", "png", "webp", "heic", "tiff", "avif", "bmp", "pdf"];
    private static readonly string[] Audio = ["mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"];
    private static readonly string[] Video = ["mp4", "mov", "mkv", "webm", "avi", "wmv"];
    private static readonly string[] Subtitles = ["srt", "vtt"];
    private static readonly string[] DocumentSources = ["docx", "pages", "md", "rtf", "rtfd", "textClipping"];
    private static readonly string[] Spreadsheets = ["xlsx", "xls", "ods", "csv", "tsv"];
    private static readonly string[] DocumentTargets = ["docx", "pages", "pdf", "txt", "md", "html", "epub", "png", "jpg"];
    private static readonly string[] Archives = ["zip", "tar", "gz", "rar"];

    /// <summary>Read, never written: not offered as a target.</summary>
    private static readonly HashSet<string> ImportOnly = ["rar", "ppt", "xls", "rtf", "rtfd", "textClipping"];

    /// <summary>Tools that run on a whole selection: the only ones the Tools wheel offers for several files.</summary>
    private static readonly HashSet<string> BatchTools =
    [
        "compress", "removeMetadata", "cropImage", "normalizeAudio", "muteVideo", "splitPDF", "extractArchive",
        "createPDF", "mergePDF", "joinVideos", "createCollage",
    ];

    /// <summary>
    /// The Convert wheel for files of <paramref name="formats"/> (one entry per file): the formats every one of them
    /// can become, in the order of the first, without any of their own. Unknown formats get nothing.
    /// </summary>
    public static IReadOnlyList<string> Targets(IReadOnlyList<string> formats)
    {
        var sources = formats.Select(Normalize).ToList();
        if (sources.Count == 0)
            return [];
        IEnumerable<string> targets = TargetsOf(sources[0]);
        foreach (var source in sources.Distinct().Skip(1))
            targets = targets.Intersect(TargetsOf(source));
        return targets.Where(t => !sources.Contains(t)).ToList();
    }

    /// <summary>
    /// The Tools wheel for files of <paramref name="formats"/> (one entry per file): one file gets its format's tools,
    /// several get the batch and multi-file tools, and files whose formats have different tools get none.
    /// </summary>
    public static IReadOnlyList<string> Tools(IReadOnlyList<string> formats)
    {
        var lists = formats.Select(Normalize).Distinct().Select(f => ToolsOf(f, formats.Count)).ToList();
        return lists.Count > 0 && lists.All(l => l.SequenceEqual(lists[0])) ? lists[0] : [];
    }

    /// <summary>A Convert slice's label: the format in capitals, the site's GZIP for a gzipped tarball.</summary>
    public static string Label(string format) => Normalize(format) == "gz" ? "GZIP" : format.ToUpperInvariant();

    /// <summary>FormatRegistry's id for a format of the site (the reverse of <see cref="Normalize"/>).</summary>
    public static string RegistryId(string format) => format == "gz" ? "tgz" : format;

    private static string Normalize(string format) => format.ToLowerInvariant() switch
    {
        "tgz" => "gz",
        "jpeg" => "jpg",
        "tif" => "tiff",
        var f => f,
    };

    private static IReadOnlyList<string> TargetsOf(string source)
    {
        string[] targets =
            ImageSources.Contains(source) ? ImageTargets
            : Audio.Contains(source) ? Audio
            : Video.Contains(source) ? [.. Video, "gif", "mp3"]
            : source == "gif" ? Video
            : source == "pdf" ? ["docx", "pages", "pptx", "xlsx", "jpg", "png", "epub", "txt", "md"]
            : source is "ppt" or "pptx" ? ["pdf", "jpg", "png"]
            : Spreadsheets.Contains(source) ? [.. Spreadsheets, "pdf"]
            : DocumentSources.Contains(source) ? DocumentTargets
            : source == "epub" ? ["pdf", "docx", "pages", "txt", "md", "html"]
            : source == "txt" ? ["docx", "pages", "pdf", "md", "html", "epub", "jpg", "png"]
            : Subtitles.Contains(source) ? [.. Subtitles, "txt"]
            : Archives.Contains(source) ? ["zip", "tar", "gz"]
            : [];
        return targets.Where(t => t != source && !ImportOnly.Contains(t)).ToList();
    }

    private static IReadOnlyList<string> ToolsOf(string source, int files)
    {
        var several = files > 1;
        string[] tools = source switch
        {
            "jpg" or "png" or "webp" or "heic" or "tiff" => several
                ? ["compress", "removeMetadata", "cropImage", "createPDF", "createCollage"]
                : ["compress", "removeMetadata", "editImage", "annotateImage", "frameImage", "cropImage", "redactImage"],
            "svg" => several ? ["createPDF", "createCollage"] : [],
            "mp3" or "m4a" or "wav" or "flac" =>
                ["compress", "removeMetadata", "normalizeAudio", "audioToVideo", "trimAudio", "audioChannels", "redactAudio"],
            "mp4" or "mov" or "mkv" => several
                ? ["compress", "removeMetadata", "muteVideo", "trimVideo", "cropVideo", "changeVideoSpeed", "joinVideos"]
                : ["compress", "removeMetadata", "muteVideo", "trimVideo", "cropVideo", "changeVideoSpeed", "videoSnapshots", "splitVideo", "redactVideo"],
            "gif" => ["removeMetadata"],
            "pdf" => several
                ? ["compress", "removeMetadata", "splitPDF", "mergePDF"]
                : ["compress", "removeMetadata", "splitPDF", "organizePDF"],
            "zip" or "tar" or "gz" or "rar" => ["extractArchive"],
            _ => [],
        };
        return several ? tools.Where(BatchTools.Contains).ToList() : tools;
    }
}
