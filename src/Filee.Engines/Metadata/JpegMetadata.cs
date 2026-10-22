// fork: a JPEG without its metadata, the compressed picture copied byte for byte. Kept: JFIF (APP0), the ICC profile
// (APP2 "ICC_PROFILE"), Adobe's colour transform (APP14, needed to decode CMYK and some RGB files) and every
// non-APP segment. Removed: Exif and XMP (APP1), IPTC (APP13), comments, the other APP segments (maker data, MPF,
// JFXX thumbnails) and anything after the picture (MPF second pictures, motion photo videos). A turned photo gets an
// Exif block with only its Orientation back.

using System.Buffers.Binary;

namespace Filee.Engines.Metadata;

internal static class JpegMetadata
{
    /// <returns>The file without metadata, or null when it is not a JPEG this can follow.</returns>
    public static byte[]? Strip(byte[] jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            return null;
        var kept = new List<(int Start, int Length)>();
        var orientation = 0;
        var insertAt = -1; // index in kept after which the Orientation block goes
        var at = 2;
        while (true)
        {
            if (at >= jpeg.Length || jpeg[at] != 0xFF)
                return null;
            while (at < jpeg.Length && jpeg[at] == 0xFF)
                at++;
            if (at >= jpeg.Length)
                return null;
            var marker = jpeg[at];
            var start = at - 1;
            at++;
            if (marker == 0xD9) // end of image: what follows is not part of the picture
            {
                kept.Add((start, 2));
                break;
            }
            if (marker is >= 0xD0 and <= 0xD7 or 0x01)
            {
                kept.Add((start, 2));
                continue;
            }
            if (at + 2 > jpeg.Length)
                return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(at));
            if (length < 2 || at + length > jpeg.Length)
                return null;
            var payload = jpeg.AsSpan(at + 2, length - 2);
            var end = at + length;

            if (marker == 0xE1 && payload.StartsWith("Exif\0\0"u8))
                orientation = Math.Max(orientation, ExifOrientation.Read(payload[6..]));
            if (Keeps(marker, payload))
            {
                kept.Add((start, end - start));
                if (marker == 0xE0 && insertAt < 0)
                    insertAt = kept.Count;
            }

            at = end;
            if (marker != 0xDA)
                continue;
            // Start of scan: the entropy-coded data runs to the next marker that is not a stuffed 0xFF or a restart.
            var scan = at;
            while (at + 1 < jpeg.Length && !(jpeg[at] == 0xFF && jpeg[at + 1] != 0x00 && jpeg[at + 1] is not (>= 0xD0 and <= 0xD7)))
                at++;
            if (at + 1 >= jpeg.Length)
                return null;
            kept.Add((scan, at - scan));
        }

        using var output = new MemoryStream(jpeg.Length);
        output.Write([0xFF, 0xD8]);
        if (insertAt < 0)
            insertAt = 0;
        for (var i = 0; i <= kept.Count; i++)
        {
            if (i == insertAt && ExifOrientation.Turns(orientation))
                WriteOrientation(output, orientation);
            if (i < kept.Count)
                output.Write(jpeg, kept[i].Start, kept[i].Length);
        }
        return output.ToArray();
    }

    private static bool Keeps(byte marker, ReadOnlySpan<byte> payload) => marker switch
    {
        0xE0 => payload.StartsWith("JFIF\0"u8),
        0xE2 => payload.StartsWith("ICC_PROFILE\0"u8),
        0xEE => payload.StartsWith("Adobe"u8),
        >= 0xE1 and <= 0xEF => false,
        0xFE => false,
        _ => true,
    };

    private static void WriteOrientation(Stream output, int orientation)
    {
        var tiff = ExifOrientation.Tiff(orientation);
        var length = 2 + 6 + tiff.Length;
        output.Write([0xFF, 0xE1, (byte)(length >> 8), (byte)length]);
        output.Write("Exif\0\0"u8);
        output.Write(tiff);
    }
}
