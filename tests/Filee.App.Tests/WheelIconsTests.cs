// fork: every Tangerine tool has a line icon, and every icon exists.

using Avalonia.Headless.XUnit;
using Filee.App.Controls;

namespace Filee.App.Tests;

public class WheelIconsTests
{
    // docs/TANGERINE-SPEC.md: the 26 tools.
    public static TheoryData<string> TangerineIds =>
    [
        "compress", "removeMetadata", "editImage", "annotateImage", "frameImage", "cropImage", "cropVideo", "redactImage",
        "redactVideo", "redactAudio", "normalizeAudio", "audioToVideo", "trimVideo", "trimAudio", "audioChannels",
        "muteVideo", "changeVideoSpeed", "videoSnapshots", "splitVideo", "joinVideos", "splitPDF", "mergePDF",
        "organizePDF", "createPDF", "createCollage", "extractArchive",
    ];

    [AvaloniaTheory]
    [MemberData(nameof(TangerineIds))]
    public void Every_tool_has_an_icon(string id)
    {
        TestServices.EnsureInitialized();

        Assert.NotNull(WheelIcons.GlyphFor(id));
        var icon = WheelIcons.Find(id);
        Assert.NotNull(icon);
        Assert.InRange(icon!.Bounds.Width, 4, 24);
        Assert.InRange(icon.Bounds.Height, 4, 24);
    }

    [AvaloniaFact]
    public void Plain_conversions_have_no_icon()
    {
        TestServices.EnsureInitialized();

        Assert.Null(WheelIcons.Find("to-png"));
        Assert.Null(WheelIcons.Find("compress-pdf")); // a preset id: wheels look icons up by Tangerine's tool ids
    }
}
