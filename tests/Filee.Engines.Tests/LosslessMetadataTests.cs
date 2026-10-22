// fork: the Tools wheel's Metadata on pictures never decodes them: every pixel of the result equals the source's,
// the metadata is gone, the colour profile stays and a turned photo keeps its Orientation.

using Filee.Core.Conversion;
using ImageMagick;

namespace Filee.Engines.Tests;

public class LosslessMetadataTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static MagickImage Photo(uint width = 320, uint height = 200)
    {
        var photo = new MagickImage("gradient:#FF8800-#0044AA", width, height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Flick test camera");
        exif.SetValue(ExifTag.Software, "Secret editor");
        photo.SetProfile(exif);
        photo.SetProfile(new XmpProfile("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description xmlns:dc=\"http://purl.org/dc/elements/1.1/\" dc:creator=\"Someone\"/></rdf:RDF></x:xmpmeta>"u8.ToArray()));
        photo.SetProfile(ColorProfiles.AdobeRGB1998);
        return photo;
    }

    private async Task<string> StripAsync(string source)
    {
        var before = ToolOperationTests.Hash(source);
        var format = Path.GetExtension(source).TrimStart('.');
        var job = await fx.ConvertAsync([source], ToolOperationTests.Operation("removeMetadata", format));
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        Assert.Equal(before, ToolOperationTests.Hash(source));
        return Assert.Single(job.Outputs);
    }

    /// <summary>Decodes both files as stored (no Orientation applied) and compares every pixel.</summary>
    private static void SamePixels(string expected, string actual)
    {
        using var a = new MagickImageCollection(expected);
        using var b = new MagickImageCollection(actual);
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal((a[i].Width, a[i].Height), (b[i].Width, b[i].Height));
            using var pa = a[i].GetPixels();
            using var pb = b[i].GetPixels();
            Assert.True(pa.ToByteArray(PixelMapping.RGBA)!.AsSpan().SequenceEqual(pb.ToByteArray(PixelMapping.RGBA)), $"frame {i} differs");
        }
    }

    private static void NoMetadata(string path)
    {
        using var image = new MagickImage(path);
        Assert.Null(image.GetXmpProfile());
        Assert.Null(image.GetIptcProfile());
        Assert.Null(image.Comment);
        if (image.GetExifProfile() is { } exif)
        {
            Assert.Null(exif.GetValue(ExifTag.Make));
            Assert.Null(exif.GetValue(ExifTag.Software));
        }
    }

    [Fact]
    public async Task A_jpeg_keeps_every_pixel_and_its_colour_profile()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "photo.jpg");
        using (var photo = Photo())
        {
            photo.Comment = "Secret comment";
            photo.Quality = 85;
            photo.Write(source, MagickFormat.Jpeg);
        }

        var output = await StripAsync(source);

        Assert.Equal(Path.Combine(folder, "photo No Metadata.jpg"), output);
        NoMetadata(output);
        using var result = new MagickImage(output);
        Assert.Null(result.GetExifProfile());
        Assert.Equal(ColorProfiles.AdobeRGB1998.ToByteArray(), result.GetColorProfile()?.ToByteArray());
        SamePixels(source, output);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length);
    }

    [Theory]
    [InlineData("jpg", MagickFormat.Jpeg)]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("webp", MagickFormat.WebP)]
    [InlineData("tiff", MagickFormat.Tiff)]
    public async Task A_turned_photo_keeps_its_orientation(string extension, MagickFormat format)
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "portrait." + extension);
        using (var photo = Photo(300, 200))
        {
            // Stored sideways, shown upright through Orientation 6 (turn 90° clockwise), like a phone photo.
            var exif = photo.GetExifProfile()!;
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            photo.SetProfile(exif);
            photo.Orientation = OrientationType.RightTop; // ImageMagick writes this into the Exif orientation
            photo.Write(source, format);
        }
        using (var check = new MagickImage(source))
            Assert.Equal(OrientationType.RightTop, check.Orientation);

        var output = await StripAsync(source);

        NoMetadata(output);
        using var result = new MagickImage(output);
        Assert.Equal(OrientationType.RightTop, result.Orientation);
        SamePixels(source, output);
        result.AutoOrient();
        Assert.Equal((200u, 300u), (result.Width, result.Height));
    }

    [Theory]
    [InlineData("png", MagickFormat.Png)]
    [InlineData("webp", MagickFormat.WebP)]
    [InlineData("tiff", MagickFormat.Tiff)]
    public async Task Other_pictures_keep_every_pixel_and_their_colour_profile(string extension, MagickFormat format)
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "picture." + extension);
        using (var photo = Photo())
        {
            photo.Comment = "Secret comment";
            photo.Write(source, format);
        }
        // TIFF keeps the comment as its description, not the Exif camera: either way there is something to remove.
        var written = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(source));
        Assert.True(written.Contains("Flick test camera") || written.Contains("Secret comment"));

        var output = await StripAsync(source);

        NoMetadata(output);
        using var result = new MagickImage(output);
        Assert.Equal(ColorProfiles.AdobeRGB1998.ToByteArray(), result.GetColorProfile()?.ToByteArray());
        SamePixels(source, output);
        Assert.DoesNotContain("Secret", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
        Assert.DoesNotContain("Flick test camera", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
    }

    [Fact]
    public async Task A_gif_keeps_its_frames_and_its_looping()
    {
        var folder = fx.NewFolder();
        var source = Path.Combine(folder, "loop.gif");
        using (var frames = new MagickImageCollection())
        {
            foreach (var colour in new[] { MagickColors.Coral, MagickColors.Teal, MagickColors.Gold })
                frames.Add(new MagickImage(colour, 60, 40) { AnimationDelay = 20 });
            frames[0].Comment = "Secret comment";
            frames.Write(source, MagickFormat.Gif);
        }

        var output = await StripAsync(source);

        Assert.DoesNotContain("Secret", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
        Assert.Contains("NETSCAPE2.0", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
        SamePixels(source, output);
    }

    [Fact]
    public async Task A_heic_keeps_every_pixel_its_colour_profile_and_its_orientation()
    {
        Assert.SkipUnless(Filee.Engines.Media.FfmpegConverter.Locate() is not null, "FFmpeg is not installed (pwsh build/fetch-engines.ps1 -Only ffmpeg)");
        var folder = fx.NewFolder();
        var jpg = Path.Combine(folder, "phone.jpg");
        using (var photo = Photo(640, 480))
            photo.Write(jpg, MagickFormat.Jpeg);
        // Flick's own HEIC writer keeps the Exif and the colour profile, as a phone's HEIC has them.
        var made = await fx.ConvertAsync([jpg], new Filee.Core.Presets.Preset { TargetFormat = "heic" });
        var source = Assert.Single(made.Outputs);
        Assert.Contains("Flick test camera", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(source)));

        var output = await StripAsync(source);

        Assert.DoesNotContain("Flick test camera", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
        Assert.Equal(new FileInfo(source).Length, new FileInfo(output).Length); // rewritten in place
        using var result = new MagickImage(output);
        Assert.Equal(ColorProfiles.AdobeRGB1998.ToByteArray(), result.GetColorProfile()?.ToByteArray());
        SamePixels(source, output);
    }

    [Fact]
    public async Task A_png_keeps_only_the_chunks_its_picture_needs()
    {
        var folder = fx.NewFolder();
        var plain = EngineFixture.MakeImage(folder, "plain.png", MagickFormat.Png);
        // A chunk a program made up, after the header: only an allowlist of chunks may pass.
        var png = File.ReadAllBytes(plain);
        var chunk = new List<byte> { 0, 0, 0, 13 };
        chunk.AddRange("prVtSecret data!!"u8.ToArray()); // type + 13 bytes
        var crc = Filee.Engines.Archives.Crc32.Compute(chunk.Skip(4).ToArray());
        chunk.AddRange([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
        var source = Path.Combine(folder, "logo.png");
        File.WriteAllBytes(source, [.. png[..33], .. chunk, .. png[33..]]);

        var output = await StripAsync(source);

        Assert.DoesNotContain("Secret", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(output)));
        SamePixels(source, output);
    }
}
