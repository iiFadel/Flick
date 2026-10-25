// fork: the editor windows' edits (Phase 5) run end to end through the job queue. Inputs are made here; the source must
// come out untouched.

using Filee.Core.Conversion;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Core.Wheels;
using ImageMagick;

namespace Filee.Engines.Tests;

public class EditorToolTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static Preset Crop(string format, NormalizedRect crop)
    {
        var preset = ToolOperations.For("cropImage", [format], _ => "Cropped") ?? throw new InvalidOperationException();
        preset.Image.Edit = new ImageEdit { Crop = crop };
        return preset;
    }

    [Theory]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("jpg", MagickFormat.Jpeg)]
    [InlineData("webp", MagickFormat.WebP)]
    [InlineData("tiff", MagickFormat.Tiff)]
    public async Task Crop_keeps_the_part_chosen_in_the_same_format(string format, MagickFormat magick)
    {
        var folder = fx.NewFolder();
        var source = EngineFixture.MakeImage(folder, "photo." + format, magick, 320, 200);
        var before = ToolOperationTests.Hash(source);

        var job = await fx.ConvertAsync([source], Crop(format, new NormalizedRect(0.25, 0.5, 0.5, 0.5)));

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, $"photo Cropped.{format}"), output);
        using var result = new MagickImage(output);
        Assert.Equal((160u, 100u), (result.Width, result.Height));
        Assert.Equal(before, ToolOperationTests.Hash(source));
    }

    [Fact]
    public async Task Crop_works_on_the_picture_as_it_looks_after_its_exif_turn()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "phone.jpg");
        using (var photo = new MagickImage(MagickColors.Coral, 320, 200))
        {
            // Stored landscape, shown turned right (EXIF orientation 6): on screen 200 × 320.
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)OrientationType.RightTop);
            photo.SetProfile(exif);
            photo.Orientation = OrientationType.RightTop; // ImageMagick writes this into the profile
            photo.Write(source, MagickFormat.Jpeg);
        }
        using (var written = new MagickImage(source))
            Assert.Equal(OrientationType.RightTop, written.Orientation);

        var job = await fx.ConvertAsync([source], Crop("jpg", new NormalizedRect(0, 0, 1, 0.5)));

        Assert.Equal(JobState.Completed, job.State);
        using var result = new MagickImage(Assert.Single(job.Outputs));
        Assert.Equal((200u, 160u), (result.Width, result.Height));
    }

    [Fact]
    public async Task Organize_writes_the_pages_in_the_new_order_turned_and_copied()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "notes.pdf");
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            foreach (var width in new[] { 100, 200, 300 })
                document.AddPage().Width = PdfSharp.Drawing.XUnit.FromPoint(width);
            document.Save(source);
        }
        var before = ToolOperationTests.Hash(source);
        var preset = ToolOperations.For("organizePDF", ["pdf"], _ => "Organized")!;
        preset.Pdf.Edit = new PdfEdit { Pages = [new PagePlacement(2), new PagePlacement(0, 90), new PagePlacement(0)] };

        var job = await fx.ConvertAsync([source], preset);

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "notes Organized.pdf"), output);
        using var result = PdfSharp.Pdf.IO.PdfReader.Open(output, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(3, result.PageCount);
        Assert.Equal([300, 100, 100], result.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(p => (int)Math.Round(p.Width.Point)));
        Assert.Equal([0, 90, 0], result.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(p => p.Rotate));
        Assert.Equal(before, ToolOperationTests.Hash(source));
    }

    [Fact]
    public void Organize_shows_every_page_as_a_small_picture()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "notes.pdf");
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage();
            document.AddPage();
            document.Save(source);
        }

        var pictures = Pdf.PdfPages.Thumbnails(source, 100, CancellationToken.None);

        Assert.Equal(2, pictures.Count);
        using var first = new MagickImage(pictures[0]);
        Assert.Equal(100u, first.Height);
    }

    [Theory]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("jpg", MagickFormat.Jpeg)]
    public async Task Redact_fills_a_box_black_and_leaves_the_rest(string format, MagickFormat magick)
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo." + format);
        using (var photo = new MagickImage(MagickColors.White, 400, 200))
            photo.Write(source, magick);
        var preset = ToolOperations.For("redactImage", [format], _ => "Redacted")!;
        preset.Image.Edit = new ImageEdit { Redactions = [new NormalizedRect(0.25, 0.25, 0.25, 0.5)], RedactStyle = RedactStyle.Solid };

        var job = await fx.ConvertAsync([source], preset);

        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, $"photo Redacted.{format}"), output);
        using var result = new MagickImage(output);
        Assert.Equal((400u, 200u), (result.Width, result.Height));
        using var pixels = result.GetPixels();
        Assert.True(pixels.GetPixel(150, 100)!.ToColor()!.R < 20); // inside the box
        Assert.True(pixels.GetPixel(50, 100)!.ToColor()!.R > 235); // outside it
    }

    [Fact]
    public async Task Redact_blurs_a_box_beyond_reading()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "stripes.png");
        using (var stripes = new MagickImage("pattern:verticalsaw", new MagickReadSettings { Width = 400, Height = 200 }))
        {
            stripes.Threshold(new Percentage(50));
            stripes.Write(source, MagickFormat.Png);
        }
        var preset = ToolOperations.For("redactImage", ["png"], _ => "Redacted")!;
        preset.Image.Edit = new ImageEdit { Redactions = [new NormalizedRect(0, 0, 0.5, 1)], RedactStyle = RedactStyle.Blur };

        var job = await fx.ConvertAsync([source], preset);

        using var original = new MagickImage(source);
        using var result = new MagickImage(Assert.Single(job.Outputs));
        using var left = result.CloneArea(10, 10, 180, 180);
        using var right = result.CloneArea(210, 10, 180, 180);
        using var rightBefore = original.CloneArea(210, 10, 180, 180);
        Assert.True(left.Statistics().Composite().StandardDeviation < right.Statistics().Composite().StandardDeviation / 4,
            "the blurred half has hardly any contrast left");
        Assert.Equal(0, right.Compare(rightBefore, ErrorMetric.Absolute)); // the other half is untouched
    }

    [Fact]
    public async Task Annotate_draws_every_mark_into_a_png()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo.jpg");
        using (var photo = new MagickImage(MagickColors.White, 800, 500))
            photo.Write(source, MagickFormat.Jpeg);
        var preset = ToolOperations.For("annotateImage", ["jpg"], _ => "Annotated")!;
        Assert.Equal("png", preset.TargetFormat);
        const string Red = "#E5484D";
        preset.Image.Edit = new ImageEdit
        {
            Annotations =
            [
                (new Annotation(AnnotationKind.Rectangle, 0.05, 0.1, 0.3, 0.5, Red), 0),
                (new Annotation(AnnotationKind.Arrow, 0.4, 0.8, 0.7, 0.6, Red), 0),
                (new Annotation(AnnotationKind.Ellipse, 0.6, 0.1, 0.9, 0.4, Red), 0),
                (new Annotation(AnnotationKind.Text, 0.05, 0.75, 0, 0, "#000000", "Look here"), 0),
                (new Annotation(AnnotationKind.Callout, 0.5, 0.3, 0, 0, Red), 1),
            ],
        };

        var job = await fx.ConvertAsync([source], preset);

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "photo Annotated.png"), output);
        using var result = new MagickImage(output);
        Assert.Equal((800u, 500u), (result.Width, result.Height));
        using var pixels = result.GetPixels();
        bool Red_(int x, int y) => pixels.GetPixel(x, y)!.ToColor() is { } c && c.R > 200 && c.G < 120;
        Assert.True(Red_(40, 150), "the box's left edge");          // x 0.05 * 800
        Assert.False(Red_(140, 150), "inside the box stays white");
        Assert.True(Red_(388, 140), "the callout's dot");             // 0.5, 0.3, beside its white number
        Assert.True(Red_(560, 300), "the arrow's head");              // the tip 0.7, 0.6
        using var text = result.CloneArea(40, 375, 200, 40);
        Assert.True(text.Statistics().Composite().Minimum < 0.3 * Quantum.Max, "the text is drawn in black");
    }

    [Theory]
    [InlineData("jpg", MagickFormat.Jpeg)]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("webp", MagickFormat.WebP)]
    public async Task Edit_writes_the_picture_brighter_and_grey_in_its_own_format(string format, MagickFormat magick)
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo." + format);
        using (var photo = new MagickImage(new MagickColor("#806040"), 200, 100))
            photo.Write(source, magick);
        var before = ToolOperationTests.Hash(source);
        var preset = ToolOperations.For("editImage", [format], _ => "Edited")!;
        preset.Image.Edit = new ImageEdit { Adjust = new Adjustments { Exposure = 50, Saturation = -100 } };

        var job = await fx.ConvertAsync([source], preset);

        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, $"photo Edited.{format}"), output);
        using var result = new MagickImage(output);
        var color = result.GetPixels().GetPixel(100, 50)!.ToColor()!;
        Assert.True(Math.Abs(color.R - color.B) < 12, $"no colour left: {color}");
        Assert.True(color.G > 0x60 + 20, $"brighter than it was: {color}");
        Assert.Equal(before, ToolOperationTests.Hash(source));
    }

    [Fact]
    public void Each_slider_moves_the_pixels_its_way()
    {
        static IMagickColor<byte> After(Adjustments adjust)
        {
            using var image = new MagickImage(new MagickColor("#806040"), 20, 20);
            Magick.ImageEdits.Adjust(image, adjust);
            return image.GetPixels().GetPixel(10, 10)!.ToColor()!;
        }

        var warm = After(new Adjustments { Warmth = 100 });
        var cool = After(new Adjustments { Warmth = -100 });
        Assert.True(warm.R > cool.R && warm.B < cool.B);
        Assert.True(After(new Adjustments { Exposure = -100 }).R < 0x50);
        var vivid = After(new Adjustments { Saturation = 100 });
        Assert.True(vivid.R - vivid.B > 0x80 - 0x40);
        var flat = After(new Adjustments { Contrast = -100 });
        Assert.True(flat.R - flat.B < 0x80 - 0x40);
    }

    [Fact]
    public async Task A_second_crop_is_numbered_without_brackets()
    {
        var folder = fx.NewFolder();
        var source = EngineFixture.MakeImage(folder, "photo.png", MagickFormat.Png, 320, 200);
        var crop = Crop("png", new NormalizedRect(0, 0, 0.5, 0.5));

        await fx.ConvertAsync([source], crop);
        var job = await fx.ConvertAsync([source], crop);

        Assert.Equal(Path.Combine(folder, "photo Cropped 2.png"), Assert.Single(job.Outputs));
    }
}
