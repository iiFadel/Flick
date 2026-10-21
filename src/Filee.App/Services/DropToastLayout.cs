// fork: where the drop toast goes and what it says (docs/TANGERINE-SPEC.md, "After the drop"). Pure, unit tested
// (DropToastLayoutTests).

using Filee.Core.Localization;

namespace Filee.App.Services;

public static class DropToastLayout
{
    /// <summary>The least room between the toast and the edge of the work area, in pixels.</summary>
    public const int Margin = 8;

    /// <summary>
    /// Top-left corner of a toast of <paramref name="width"/> × <paramref name="height"/> centred on the drop point,
    /// moved in to stay <see cref="Margin"/> inside the work area. All in physical pixels.
    /// </summary>
    public static (int X, int Y) Position(int dropX, int dropY, int width, int height,
        int areaX, int areaY, int areaWidth, int areaHeight) =>
    (
        Math.Clamp(dropX - width / 2, areaX + Margin, Math.Max(areaX + Margin, areaX + areaWidth - width - Margin)),
        Math.Clamp(dropY - height / 2, areaY + Margin, Math.Max(areaY + Margin, areaY + areaHeight - height - Margin))
    );

    /// <summary>"Converting to MP3" for a format; a tool's own name ("Split") for a tool.</summary>
    public static string Title(ILocalizer loc, string label, bool conversion) =>
        conversion ? loc.Format("droptoast.converting", label) : label;

    /// <summary>
    /// The second line: while running, the file (or how many) and the percentage, or the browser download's speed;
    /// when done, how many of the files were saved.
    /// </summary>
    public static string Status(ILocalizer loc, string? fileName, int files, double percent, int saved, bool finished, string? download) =>
        finished ? loc.Format("droptoast.saved", saved, files)
        : download ?? $"{fileName ?? loc.Format("home.files", files)} · {percent:0}%";
}
