// fork: the Tools wheel's operations are same-format presets with the tool's options and Tangerine's names.

using Filee.Core.Presets;
using Filee.Core.Wheels;

namespace Filee.Core.Tests;

public class ToolOperationsTests
{
    private static Preset? For(string tool, params string[] formats) =>
        ToolOperations.For(tool, formats, id => id switch
        {
            "removeMetadata" => "No Metadata",
            "muteVideo" => "Muted",
            "compress" => "Compressed",
            "normalizeAudio" => "Normalized",
            "extractArchive" => "Extracted",
            _ => "?",
        });

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
    [InlineData("jpg", 70, false)]
    [InlineData("webp", 70, false)]
    [InlineData("heic", 70, false)]
    [InlineData("png", 90, true)]
    public void Compress_shrinks_pictures_in_their_own_format(string format, int quality, bool palette)
    {
        var preset = For("compress", format)!;
        Assert.Equal(BuiltInData.SameAsSource, preset.TargetFormat);
        Assert.Equal(quality, preset.Image.Quality);
        Assert.Equal(palette, preset.Image.ReduceColors);
        Assert.True(preset.Image.KeepMetadata);
        Assert.True(preset.Output.NeverLarger);
        Assert.Equal("{name} Compressed", preset.Output.FileNamePattern);
    }

    [Fact]
    public void Compress_makes_videos_and_songs_small_and_a_wav_flac()
    {
        Assert.Equal(MediaQuality.Small, For("compress", "mov")!.Media.Quality);
        var song = For("compress", "mp3")!;
        Assert.Equal((BuiltInData.SameAsSource, 128), (song.TargetFormat, song.Media.AudioBitrateKbps));
        Assert.Equal("flac", For("compress", "wav", "wav")!.TargetFormat);
        Assert.Equal(BuiltInData.SameAsSource, For("compress", "wav", "mp3")!.TargetFormat);
        Assert.Equal(TiffCompression.Zip, For("compress", "tiff")!.Image.TiffCompression);
    }

    [Fact]
    public void Compress_on_a_pdf_is_compress_pdf_keeping_the_smaller_file_itself()
    {
        var preset = For("compress", "pdf")!;
        Assert.Equal(PdfCompression.Ebook, preset.Pdf.Compression);
        Assert.True(preset.Pdf.CompressWithGhostscript);
        Assert.False(preset.Output.NeverLarger);
    }

    [Fact]
    public void Normalize_and_extract()
    {
        Assert.True(For("normalizeAudio", "flac")!.Media.NormalizeLoudness);
        var extract = For("extractArchive", "zip")!;
        Assert.Equal(Filee.Core.Formats.FormatRegistry.Folder, extract.TargetFormat);
        Assert.Equal("{name} Extracted", extract.Output.FileNamePattern);
        Assert.NotNull(For("extractArchive", "tgz"));
    }

    [Theory]
    [InlineData("compress", "gif")]
    [InlineData("compress", "docx")]
    [InlineData("normalizeAudio", "mp4")]
    [InlineData("extractArchive", "pdf")]
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
