// fork: a PNG (or APNG) without its metadata. Only the chunks the picture needs are copied, as they are: the
// picture and its palette, transparency, colour (iCCP, sRGB, gAMA, cHRM, cICP, sBIT, bKGD), pixel density and
// animation. Everything else goes: text (tEXt, zTXt, iTXt, which also carry XMP), Exif, the modification time and
// any chunk a program made up. A turned photo gets an eXIf with only its Orientation back.

using System.Buffers.Binary;
using Filee.Engines.Archives;

namespace Filee.Engines.Metadata;

internal static class PngMetadata
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly HashSet<string> Kept =
    [
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "cICP", "mDCV", "cLLI", "sBIT", "bKGD", "pHYs",
        "acTL", "fcTL", "fdAT",
    ];

    /// <returns>The file without metadata, or null when it is not a PNG this can follow.</returns>
    public static byte[]? Strip(byte[] png)
    {
        if (png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            return null;
        using var output = new MemoryStream(png.Length);
        output.Write(Signature);
        var at = Signature.Length;
        while (at + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
            if (length > int.MaxValue - 12 || at + 12 + (long)length > png.Length)
                return null;
            var type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            var size = 12 + (int)length;
            if (type == "eXIf" && ExifOrientation.Read(png.AsSpan(at + 8, (int)length)) is var orientation && ExifOrientation.Turns(orientation))
                WriteChunk(output, "eXIf", ExifOrientation.Tiff(orientation));
            else if (Kept.Contains(type))
                output.Write(png, at, size);
            at += size;
            if (type == "IEND")
                return output.ToArray();
        }
        return null;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(type, header.AsSpan(4));
        output.Write(header);
        output.Write(data);
        var crc = new Crc32();
        crc.Append(header.AsSpan(4));
        crc.Append(data);
        var sum = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sum, crc.Value);
        output.Write(sum);
    }
}
