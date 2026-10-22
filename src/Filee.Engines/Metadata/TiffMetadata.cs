// fork: a TIFF without its metadata, changed in place so the strips and tiles stay exactly where and what they are.
// In every page's IFD the metadata entries are taken out (the rest move up, in order) and the bytes they pointed to
// are overwritten with zeros: Exif and GPS (with everything in them, maker notes too), XMP, IPTC, Photoshop data,
// and the camera, software, author, date and description texts. Orientation, the ICC profile and everything the
// pixels need stay. BigTIFF is not handled.

namespace Filee.Engines.Metadata;

internal static class TiffMetadata
{
    private const ushort ExifIfd = 34665, GpsIfd = 34853, InteropIfd = 40965;

    /// <summary>Entries removed from a page: texts, dates, metadata blocks and the Exif / GPS pointers.</summary>
    private static readonly HashSet<ushort> Removed =
    [
        270, 271, 272, 305, 306, 315, 316, 33432, // description, make, model, software, date, artist, host, copyright
        700, 33723, 34377, 50341, // XMP, IPTC, Photoshop, PrintIM
        ExifIfd, GpsIfd,
    ];

    /// <returns>The file without metadata, or null when it is not a TIFF this can follow.</returns>
    public static byte[]? Strip(byte[] source)
    {
        if (source.Length < 8 || !ExifOrientation.TryEndian(source, out var little))
            return null;
        var tiff = (byte[])source.Clone();
        var visited = new HashSet<uint>();
        var ifd = ExifOrientation.U32(tiff, 4, little);
        while (ifd != 0)
        {
            if (!visited.Add(ifd) || !TryEntries(tiff, ifd, little, out var count))
                return null;
            var entries = new List<byte[]>();
            for (var i = 0; i < count; i++)
            {
                var entry = (int)ifd + 2 + i * 12;
                var tag = ExifOrientation.U16(tiff, entry, little);
                if (!Removed.Contains(tag))
                {
                    entries.Add(tiff.AsSpan(entry, 12).ToArray());
                    continue;
                }
                if (tag is ExifIfd or GpsIfd && !ClearIfd(tiff, ExifOrientation.U32(tiff, entry + 8, little), little, visited))
                    return null;
                if (!ClearValue(tiff, entry, little))
                    return null;
            }
            var next = ExifOrientation.U32(tiff, (int)ifd + 2 + count * 12, little);
            // The kept entries, then the next-IFD offset, then zeros where the removed entries were.
            var at = (int)ifd;
            Array.Clear(tiff, at, 2 + count * 12 + 4);
            Write16(tiff, at, (ushort)entries.Count, little);
            for (var i = 0; i < entries.Count; i++)
                entries[i].CopyTo(tiff, at + 2 + i * 12);
            Write32(tiff, at + 2 + entries.Count * 12, next, little);
            ifd = next;
        }
        return tiff;
    }

    /// <summary>Zeros an Exif, GPS or interoperability IFD: its values, then its entries.</summary>
    private static bool ClearIfd(byte[] tiff, uint ifd, bool little, HashSet<uint> visited)
    {
        if (ifd == 0)
            return true;
        if (!visited.Add(ifd) || !TryEntries(tiff, ifd, little, out var count))
            return false;
        for (var i = 0; i < count; i++)
        {
            var entry = (int)ifd + 2 + i * 12;
            if (ExifOrientation.U16(tiff, entry, little) == InteropIfd
                && !ClearIfd(tiff, ExifOrientation.U32(tiff, entry + 8, little), little, visited))
                return false;
            if (!ClearValue(tiff, entry, little))
                return false;
        }
        Array.Clear(tiff, (int)ifd, 2 + count * 12 + 4);
        return true;
    }

    /// <summary>Zeros the bytes an entry's value takes outside the entry (values of 4 bytes or less are inside it).</summary>
    private static bool ClearValue(byte[] tiff, int entry, bool little)
    {
        var size = TypeSize(ExifOrientation.U16(tiff, entry + 2, little)) * (long)ExifOrientation.U32(tiff, entry + 4, little);
        if (size <= 4)
            return true;
        var offset = ExifOrientation.U32(tiff, entry + 8, little);
        if (offset + size > tiff.Length)
            return false;
        Array.Clear(tiff, (int)offset, (int)size);
        return true;
    }

    private static bool TryEntries(byte[] tiff, uint ifd, bool little, out int count)
    {
        count = 0;
        if (ifd < 8 || ifd + 2L > tiff.Length)
            return false;
        count = ExifOrientation.U16(tiff, (int)ifd, little);
        return ifd + 2L + count * 12L + 4 <= tiff.Length;
    }

    private static long TypeSize(ushort type) => type switch
    {
        1 or 2 or 6 or 7 => 1,
        3 or 8 => 2,
        4 or 9 or 11 or 13 => 4,
        5 or 10 or 12 => 8,
        _ => 1,
    };

    private static void Write16(byte[] data, int at, ushort value, bool little)
    {
        if (little)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), value);
        else
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), value);
    }

    private static void Write32(byte[] data, int at, uint value, bool little)
    {
        if (little)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), value);
        else
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), value);
    }
}
