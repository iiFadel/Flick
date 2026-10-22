// fork: a WebP without its metadata, the picture chunks copied as they are. Only the extended format (VP8X) holds
// metadata: its EXIF and "XMP " chunks go, and so do their flags; the ICC profile (ICCP) stays. A turned photo gets an
// EXIF chunk with only its Orientation back.

using System.Buffers.Binary;

namespace Filee.Engines.Metadata;

internal static class WebpMetadata
{
    private const byte ExifFlag = 0x08;
    private const byte XmpFlag = 0x04;

    /// <returns>The file without metadata, or null when it is not a WebP this can follow.</returns>
    public static byte[]? Strip(byte[] webp)
    {
        if (webp.Length < 12 || !webp.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !webp.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return null;
        var riffEnd = Math.Min(webp.Length, 8 + (long)BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(4)));
        var chunks = new List<(string Type, int Start, int Size)>();
        var orientation = 0;
        var at = 12;
        while (at + 8 <= riffEnd)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(at + 4));
            var size = 8 + (long)length + (length & 1);
            if (at + 8 + (long)length > riffEnd)
                return null;
            var type = System.Text.Encoding.ASCII.GetString(webp, at, 4);
            if (type == "EXIF")
                orientation = ExifOrientation.Read(Tiff(webp.AsSpan(at + 8, (int)length)));
            chunks.Add((type, at, (int)Math.Min(size, riffEnd - at)));
            at += (int)size;
        }
        if (chunks.Count == 0)
            return null;
        if (chunks[0].Type != "VP8X")
            return webp[..(int)riffEnd]; // simple WebP: no room for metadata

        var turned = ExifOrientation.Turns(orientation);
        using var output = new MemoryStream(webp.Length);
        output.Write(webp, 0, 12);
        foreach (var (type, start, size) in chunks)
        {
            if (type == "VP8X")
            {
                var header = webp.AsSpan(start, size).ToArray();
                header[8] = (byte)(header[8] & ~(XmpFlag | ExifFlag) | (turned ? ExifFlag : 0));
                output.Write(header);
            }
            else if (type == "EXIF" && turned)
                WriteChunk(output, "EXIF", ExifOrientation.Tiff(orientation));
            else if (type is not ("EXIF" or "XMP "))
                output.Write(webp, start, size);
        }
        var result = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        return result;
    }

    /// <summary>The TIFF structure of an EXIF chunk (some writers put "Exif\0\0" before it).</summary>
    private static ReadOnlySpan<byte> Tiff(ReadOnlySpan<byte> exif) => exif.StartsWith("Exif\0\0"u8) ? exif[6..] : exif;

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var header = new byte[8];
        System.Text.Encoding.ASCII.GetBytes(type, header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)data.Length);
        output.Write(header);
        output.Write(data);
        if ((data.Length & 1) != 0)
            output.WriteByte(0);
    }
}
