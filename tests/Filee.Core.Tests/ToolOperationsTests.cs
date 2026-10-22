// fork: the Tools wheel's operations are same-format presets with the tool's options and Tangerine's names.

using Filee.Core.Presets;
using Filee.Core.Wheels;

namespace Filee.Core.Tests;

public class ToolOperationsTests
{
    private static Preset? For(string tool, params string[] formats) =>
        ToolOperations.For(tool, formats, id => id == "removeMetadata" ? "No Metadata" : id == "muteVideo" ? "Muted" : "?");

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    [InlineData("webp")]
    [InlineData("heic")]
    [InlineData("tiff")]
    [InlineData("gif")]
    public void Metadata_of_a_picture_keeps_its_format_and_drops_the_tags(string format)
    {
        var preset = For("removeMetadata", format);
        Assert.NotNull(preset);
        Assert.Equal(BuiltInData.SameAsSource, preset.TargetFormat);
        Assert.Equal(PresetKind.Convert, preset.Kind);
        Assert.False(preset.Image.KeepMetadata);
        Assert.True(preset.Image.KeepColorProfile);
        Assert.Equal("{name} No Metadata", preset.Output.FileNamePattern);
        Assert.True(preset.Output.PlainCopyNumbers);
    }

    [Fact]
    public void A_jpeg_loses_no_quality_to_its_metadata()
    {
        Assert.Equal(100, For("removeMetadata", "jpg")!.Image.Quality);
        Assert.Equal(100, For("removeMetadata", "jpg", "jpg")!.Image.Quality);
        Assert.Equal(new ImageOptions().Quality, For("removeMetadata", "jpg", "webp")!.Image.Quality);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("m4a")]
    [InlineData("wav")]
    [InlineData("flac")]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("mkv")]
    public void Metadata_of_audio_and_video_strips_the_tags(string format)
    {
        var preset = For("removeMetadata", format);
        Assert.NotNull(preset);
        Assert.Equal(BuiltInData.SameAsSource, preset.TargetFormat);
        Assert.True(preset.Media.StripMetadata);
        Assert.False(preset.Media.RemoveAudio);
    }

    [Fact]
    public void Metadata_of_a_pdf_removes_the_document_information()
    {
        var preset = For("removeMetadata", "pdf", "pdf");
        Assert.NotNull(preset);
        Assert.True(preset.Pdf.RemoveMetadata);
        Assert.Equal(PdfCompression.None, preset.Pdf.Compression);
        Assert.Equal("op-removeMetadata", preset.Id);
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("mkv")]
    public void Mute_drops_the_sound_of_a_video(string format)
    {
        var preset = For("muteVideo", format, format);
        Assert.NotNull(preset);
        Assert.Equal(BuiltInData.SameAsSource, preset.TargetFormat);
        Assert.True(preset.Media.RemoveAudio);
        Assert.Equal("{name} Muted", preset.Output.FileNamePattern);
    }

    [Theory]
    [InlineData("muteVideo", "mp3")]
    [InlineData("removeMetadata", "docx")]
    [InlineData("removeMetadata", "jpg", "mp4")]
    [InlineData("editImage", "jpg")]
    [InlineData("trimVideo", "mp4")]
    public void Tools_Flick_cant_run_there_have_no_operation(string tool, params string[] formats) =>
        Assert.Null(For(tool, formats));

    [Fact]
    public void No_files_no_operation() => Assert.Null(For("removeMetadata"));

    [Fact]
    public void A_taken_name_is_numbered_the_way_Tangerine_does()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.jpg");
        var rule = For("removeMetadata", "jpg")!.Output;
        rule.Location = OutputLocation.SameFolder;
        var tokens = new OutputPathResolver.Tokens(source, "Metadata", 1, DateTime.Now);

        Assert.Equal(Path.Combine(dir.Path, "photo No Metadata.jpg"), OutputPathResolver.Resolve(rule, tokens, "jpg", File.Exists));
        dir.File("photo No Metadata.jpg");
        Assert.Equal(Path.Combine(dir.Path, "photo No Metadata 2.jpg"), OutputPathResolver.Resolve(rule, tokens, "jpg", File.Exists));
    }

    [Fact]
    public void Every_call_is_a_fresh_preset()
    {
        var first = For("muteVideo", "mp4")!;
        first.Media.RemoveAudio = false;
        Assert.True(For("muteVideo", "mp4")!.Media.RemoveAudio);
    }
}
