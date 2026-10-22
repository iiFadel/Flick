// fork: the one Exif value lossless metadata removal keeps: Orientation (tag 274), so a photo stored sideways and shown
// upright still shows upright. Reads it from a TIFF structure (Exif blocks are one) and writes the smallest Exif
// block that holds only it.

using System.Buffers.Binary;

namespace Filee.Engines.Metadata;

internal static class ExifOrientation
{
    private const ushort OrientationTag = 274;

    /// <summary>The Orientation in <paramref name="tiff"/> (a TIFF header and its first IFD), or 0 when it has none.</summary>
    public static int Read(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8 || !TryEndian(tiff, out var little))
            return 0;
        var ifd = U32(tiff, 4, little);
        if (ifd > (uint)tiff.Length - 2)
            return 0;
        var count = U16(tiff, (int)ifd, little);
        for (var i = 0; i < count; i++)
        {
            var entry = (int)ifd + 2 + i * 12;
            if (entry + 12 > tiff.Length)
                return 0;
            if (U16(tiff, entry, little) == OrientationTag && U16(tiff, entry + 2, little) == 3)
                return U16(tiff, entry + 8, little);
        }
        return 0;
    }

    /// <summary>True for an Orientation that turns or mirrors the picture (2 … 8).</summary>
    public static bool Turns(int orientation) => orientation is >= 2 and <= 8;

    /// <summary>A little-endian TIFF structure with one IFD holding only <paramref name="orientation"/>.</summary>
    public static byte[] Tiff(int orientation)
    {
        var tiff = new byte[26];
        "II*\0"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(10), OrientationTag);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(12), 3); // SHORT
        BinaryPrimitives.WriteUInt32LittleEndian(tiff.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff.AsSpan(18), (ushort)orientation);
        return tiff; // the next-IFD offset (bytes 22 … 25) stays 0
    }

    internal static bool TryEndian(ReadOnlySpan<byte> tiff, out bool little)
    {
        little = tiff[0] == 'I';
        return tiff.Length >= 4 && (tiff[..2].SequenceEqual("II"u8) || tiff[..2].SequenceEqual("MM"u8)) && U16(tiff, 2, little) == 42;
    }

    internal static ushort U16(ReadOnlySpan<byte> data, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt16BigEndian(data[at..]);

    internal static uint U32(ReadOnlySpan<byte> data, int at, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
}
