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
        ToolOperations.For(tool, [format], id => id == "removeMetadata" ? "No Metadata" : "Muted")
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
}
