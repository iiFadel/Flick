// fork: the wheels' contents are exactly docs/TANGERINE-SPEC.md's lists, in order.

using Filee.Core.Wheels;

namespace Filee.Core.Tests;

public class WheelCatalogTests
{
    // One demo file per category, as the spec lists them ("Wheel contents").
    public static TheoryData<string, string[], string[]> Categories => new()
    {
        { "jpg", ["png", "webp", "heic", "tiff", "avif", "bmp", "pdf"], ["compress", "removeMetadata", "editImage", "annotateImage", "frameImage", "cropImage", "redactImage"] },
        { "mov", ["mp4", "mkv", "webm", "avi", "wmv", "gif", "mp3"], ["compress", "removeMetadata", "muteVideo", "trimVideo", "cropVideo", "changeVideoSpeed", "videoSnapshots", "splitVideo", "redactVideo"] },
        { "mp3", ["m4a", "wav", "flac", "ogg", "opus", "aiff", "wma"], ["compress", "removeMetadata", "normalizeAudio", "audioToVideo", "trimAudio", "audioChannels", "redactAudio"] },
        { "pdf", ["docx", "pages", "pptx", "xlsx", "jpg", "png", "epub", "txt", "md"], ["compress", "removeMetadata", "splitPDF", "organizePDF"] },
        { "docx", ["pages", "pdf", "txt", "md", "html", "epub", "png", "jpg"], [] },
        { "pptx", ["pdf", "jpg", "png"], [] },
        { "xlsx", ["ods", "csv", "tsv", "pdf"], [] },
        { "epub", ["pdf", "docx", "pages", "txt", "md", "html"], [] },
        { "srt", ["vtt", "txt"], [] },
        { "zip", ["tar", "gz"], ["extractArchive"] },
    };

    [Theory]
    [MemberData(nameof(Categories))]
    public void Each_category_has_the_specs_wheels(string source, string[] convert, string[] tools)
    {
        Assert.Equal(convert, WheelCatalog.Targets([source]));
        Assert.Equal(tools, WheelCatalog.Tools([source]));
    }

    [Theory]
    [MemberData(nameof(Categories))]
    public void A_wheel_never_offers_the_files_own_format_or_anything_twice(string source, string[] _, string[] __)
    {
        var targets = WheelCatalog.Targets([source]);
        Assert.DoesNotContain(source, targets);
        Assert.Equal(targets.Distinct(), targets);
        Assert.Equal(WheelCatalog.Tools([source]).Distinct(), WheelCatalog.Tools([source]));
    }

    [Theory]
    [InlineData("svg", new string[0])]
    [InlineData("avif", new string[0])]
    [InlineData("ogg", new string[0])]
    [InlineData("webm", new string[0])]
    [InlineData("gif", new[] { "removeMetadata" })]
    [InlineData("tgz", new[] { "extractArchive" })]
    public void Formats_outside_the_demo_follow_the_site(string source, string[] tools) =>
        Assert.Equal(tools, WheelCatalog.Tools([source]));

    [Fact]
    public void Several_files_get_the_batch_tools_and_the_multi_file_ones()
    {
        Assert.Equal(["compress", "removeMetadata", "cropImage", "createPDF", "createCollage"], WheelCatalog.Tools(["jpg", "jpg"]));
        Assert.Equal(["compress", "removeMetadata", "splitPDF", "mergePDF"], WheelCatalog.Tools(["pdf", "pdf"]));
        Assert.Equal(["compress", "removeMetadata", "muteVideo", "joinVideos"], WheelCatalog.Tools(["mov", "mp4"]));
        Assert.Equal(["createPDF", "createCollage"], WheelCatalog.Tools(["svg", "svg"]));
    }

    [Fact]
    public void Mixed_files_get_what_every_one_of_them_can_become()
    {
        // JPG and PNG: the image targets both can become, neither of their own formats.
        Assert.Equal(["webp", "heic", "tiff", "avif", "bmp", "pdf"], WheelCatalog.Targets(["jpg", "png"]));
        Assert.Equal(["compress", "removeMetadata", "cropImage", "createPDF", "createCollage"], WheelCatalog.Tools(["jpg", "png"]));
        // A photo and a song share nothing.
        Assert.Empty(WheelCatalog.Targets(["jpg", "mp3"]));
        Assert.Empty(WheelCatalog.Tools(["jpg", "mp3"]));
        Assert.Empty(WheelCatalog.Targets(["dwg"]));
    }

    [Theory]
    [InlineData("png", "PNG")]
    [InlineData("gz", "GZIP")]
    [InlineData("tiff", "TIFF")]
    public void Format_names_are_the_slices_labels(string format, string label) =>
        Assert.Equal(label, WheelCatalog.Label(format));
}
