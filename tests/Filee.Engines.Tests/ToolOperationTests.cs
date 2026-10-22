// fork: the Tools wheel's operations run end to end on pictures and PDFs (the video and audio ones are in
// MediaTests.Tools.cs, as they need FFmpeg). Inputs are made here; the source must come out untouched.

using System.Security.Cryptography;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Core.Wheels;
using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Filee.Engines.Tests;

public class ToolOperationTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    internal static Preset Operation(string tool, string format) =>
        ToolOperations.For(tool, [format], id => id switch
        {
            "removeMetadata" => "No Metadata",
            "muteVideo" => "Muted",
            "compress" => "Compressed",
            "normalizeAudio" => "Normalized",
            _ => "Extracted",
        })
        ?? throw new InvalidOperationException($"No {tool} for {format}.");

    internal static byte[] Hash(string path) => SHA256.HashData(File.ReadAllBytes(path));

    [Fact]
    public async Task Metadata_removes_the_exif_of_a_photo_and_keeps_its_quality()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo.jpg");
        using (var photo = new MagickImage(MagickColors.Coral, 320, 200))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Make, "Flick test");
            photo.SetProfile(exif);
            photo.Quality = 80;
            photo.Write(source, MagickFormat.Jpeg);
        }
        var before = Hash(source);

        var job = await fx.ConvertAsync([source], Operation("removeMetadata", "jpg"));

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "photo No Metadata.jpg"), output);
        using var result = new MagickImage(output);
        Assert.Null(result.GetExifProfile());
        Assert.InRange(result.Quality, 1u, 80u);
        Assert.Equal(before, Hash(source));
    }

    [Fact]
    public async Task Metadata_removes_the_document_information_of_a_pdf()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "notes.pdf");
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.AddPage();
            document.Info.Title = "Secret title";
            document.Info.Author = "Someone";
            document.Info.Keywords = "private";
            document.Save(source);
        }
        var before = Hash(source);

        var job = await fx.ConvertAsync([source], Operation("removeMetadata", "pdf"));

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "notes No Metadata.pdf"), output);
        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(2, result.PageCount);
        Assert.Equal("", result.Info.Title);
        Assert.Equal("", result.Info.Author);
        Assert.Equal("", result.Info.Keywords);
        Assert.Equal(before, Hash(source));
    }

    [Fact]
    public async Task A_second_run_is_numbered_like_Tangerine()
    {
        var folder = fx.NewFolder();
        var source = EngineFixture.MakeImage(folder, "logo.png", MagickFormat.Png);
        await fx.ConvertAsync([source], Operation("removeMetadata", "png"));

        var job = await fx.ConvertAsync([source], Operation("removeMetadata", "png"));

        Assert.Equal(Path.Combine(folder, "logo No Metadata 2.png"), Assert.Single(job.Outputs));
    }

    [Fact]
    public async Task Compress_makes_a_photo_smaller_in_its_own_format()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo.jpg");
        using (var photo = new MagickImage("plasma:", 800, 600))
        {
            photo.Quality = 95;
            photo.Write(source, MagickFormat.Jpeg);
        }

        var job = await fx.ConvertAsync([source], Operation("compress", "jpg"));

        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "photo Compressed.jpg"), output);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length * 0.8);
        Assert.Null(Assert.Single(job.Files).NoticeKey);
    }

    [Fact]
    public async Task Compress_turns_a_png_into_a_palette_picture_with_its_transparency()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "shot.png");
        using (var shot = new MagickImage("plasma:", 600, 400))
        {
            shot.Alpha(AlphaOption.Set);
            using (var area = shot.GetPixels())
                area.SetArea(0, 0, 40, 40, new byte[40 * 40 * shot.ChannelCount]);
            shot.Write(source, MagickFormat.Png);
        }

        var output = Assert.Single((await fx.ConvertAsync([source], Operation("compress", "png"))).Outputs);

        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length / 2);
        using var result = new MagickImage(output);
        Assert.True(result.TotalColors <= 256);
        using var pixels = result.GetPixels();
        Assert.Equal(0, pixels.GetPixel(5, 5).GetChannel(3));
    }

    [Fact]
    public async Task A_file_compress_cant_shrink_is_copied_as_it_is_with_a_notice()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "tiny.jpg");
        using (var tiny = new MagickImage(MagickColors.Coral, 16, 16))
        {
            tiny.Quality = 20;
            tiny.Write(source, MagickFormat.Jpeg);
        }

        var job = await fx.ConvertAsync([source], Operation("compress", "jpg"));

        var output = Assert.Single(job.Outputs);
        Assert.Equal(Hash(source), Hash(output));
        Assert.Equal("notice.not_smaller", Assert.Single(job.Files).NoticeKey);
    }

    [Fact]
    public async Task Compress_on_a_pdf_writes_name_compressed()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "scan.pdf");
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.Save(source);
        }

        var job = await fx.ConvertAsync([source], Operation("compress", "pdf"));

        Assert.Equal(JobState.Completed, job.State);
        Assert.Equal(Path.Combine(folder, "scan Compressed.pdf"), Assert.Single(job.Outputs));
    }

    [Fact]
    public async Task Extract_unpacks_into_name_extracted()
    {
        Assert.SkipUnless(fx.Catalog.StatusOf(fx.Converters.Single(c => c.Id == "archive")).IsAvailable,
            "7-Zip not found (pwsh build/fetch-engines.ps1 -Only 7zip)");
        var folder = fx.NewFolder();
        var content = Directory.CreateDirectory(Path.Combine(folder, "content")).FullName;
        File.WriteAllText(Path.Combine(content, "notes.txt"), "hello");
        var source = Path.Combine(folder, "project.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(content, source);

        var job = await fx.ConvertAsync([source], Operation("extractArchive", "zip"));

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "project Extracted"), output);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(output, "notes.txt")));
    }
}
