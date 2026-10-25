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
