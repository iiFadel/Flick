// fork: a HEIC / HEIF / AVIF without its metadata, changed in place so the coded pictures stay exactly where and what
// they are. The Exif and XMP items' bytes are overwritten with zeros and their item type renamed to one no reader
// knows ("Flk0"), so readers skip them. Orientation lives in the picture's irot / imir properties in HEIF (readers
// ignore Exif's), so it stays. The ICC profile (colr property) stays too.

using System.Buffers.Binary;
using System.Text;

namespace Filee.Engines.Metadata;

internal static class HeifMetadata
{
    /// <returns>The file without metadata, or null when it is not a HEIF file this can follow.</returns>
    public static byte[]? Strip(byte[] source)
    {
        var file = (byte[])source.Clone();
        if (Find(file, 0, file.Length, "ftyp") is not (0, _) || Find(file, 0, file.Length, "meta") is not ({ } meta, var metaEnd))
            return null;
        var children = meta + 4; // meta is a full box
        if (Find(file, children, metaEnd, "iinf") is not ({ } iinf, var iinfEnd)
            || Find(file, children, metaEnd, "iloc") is not ({ } iloc, var ilocEnd))
            return null;
        var idat = Find(file, children, metaEnd, "idat");

        var metadata = MetadataItems(file, iinf, iinfEnd);
        if (metadata is null)
            return null;
        if (metadata.Count == 0)
            return file;
        foreach (var (_, typeAt) in metadata)
            "Flk0"u8.CopyTo(file.AsSpan(typeAt));
        return ClearPayloads(file, iloc, ilocEnd, idat.Start, metadata.Select(m => m.Id).ToHashSet()) ? file : null;
    }

    /// <summary>The Exif and XMP items: their id and where their item type is written.</summary>
    private static List<(uint Id, int TypeAt)>? MetadataItems(byte[] file, int iinf, int end)
    {
        var version = file[iinf];
        var at = iinf + 4 + (version == 0 ? 2 : 4);
        var items = new List<(uint, int)>();
        while (at + 8 <= end)
        {
            var (size, header) = BoxSize(file, at, end);
            if (size <= 0)
                return null;
            if (Encoding.ASCII.GetString(file, at + 4, 4) == "infe")
            {
                var body = at + header;
                var infeVersion = file[body];
                if (infeVersion >= 2)
                {
                    var p = body + 4;
                    var id = infeVersion == 2 ? BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(p)) : BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(p));
                    p += (infeVersion == 2 ? 2 : 4) + 2; // + item_protection_index
                    var typeAt = p;
                    var type = Encoding.ASCII.GetString(file, p, 4);
                    p += 4;
                    ReadString(file, ref p, at + (int)size); // item_name
                    var contentType = type == "mime" ? ReadString(file, ref p, at + (int)size) : "";
                    if (type == "Exif" || type == "mime" && contentType.Contains("xmp", StringComparison.OrdinalIgnoreCase)
                        || type == "mime" && contentType.Contains("rdf+xml", StringComparison.OrdinalIgnoreCase))
                        items.Add((id, typeAt));
                }
            }
            at += (int)size;
        }
        return items;
    }

    /// <summary>Zeros the extents of <paramref name="ids"/> listed in the iloc box (<paramref name="idat"/>: where the idat box's data starts).</summary>
    private static bool ClearPayloads(byte[] file, int iloc, int end, int? idat, HashSet<uint> ids)
    {
        var version = file[iloc];
        var p = iloc + 4;
        if (p + 2 > end)
            return false;
        var offsetSize = file[p] >> 4;
        var lengthSize = file[p] & 15;
        var baseOffsetSize = file[p + 1] >> 4;
        var indexSize = version is 1 or 2 ? file[p + 1] & 15 : 0;
        p += 2;
        var count = version < 2 ? Read(file, ref p, 2) : Read(file, ref p, 4);
        for (ulong i = 0; i < count; i++)
        {
            var id = (uint)(version < 2 ? Read(file, ref p, 2) : Read(file, ref p, 4));
            var method = version is 1 or 2 ? (int)(Read(file, ref p, 2) & 15) : 0;
            Read(file, ref p, 2); // data_reference_index
            var baseOffset = Read(file, ref p, baseOffsetSize);
            var extents = Read(file, ref p, 2);
            for (ulong e = 0; e < extents; e++)
            {
                if (indexSize > 0)
                    Read(file, ref p, indexSize);
                var offset = Read(file, ref p, offsetSize);
                var length = Read(file, ref p, lengthSize);
                if (p > end)
                    return false;
                if (!ids.Contains(id))
                    continue;
                ulong start = method switch
                {
                    0 => baseOffset + offset,
                    1 when idat is { } data => (ulong)data + baseOffset + offset,
                    _ => ulong.MaxValue,
                };
                if (start == ulong.MaxValue || start > (ulong)file.Length)
                    return false;
                var size = length == 0 ? (ulong)file.Length - start : length;
                if (start + size > (ulong)file.Length)
                    return false;
                Array.Clear(file, (int)start, (int)size);
            }
        }
        return p <= end;
    }

    /// <summary>The first box of <paramref name="type"/> between <paramref name="from"/> and <paramref name="to"/>: where its body starts and where it ends.</summary>
    private static (int? Start, int End) Find(byte[] file, int from, int to, string type)
    {
        var at = from;
        while (at + 8 <= to)
        {
            var (size, header) = BoxSize(file, at, to);
            if (size <= 0)
                return (null, 0);
            if (Encoding.ASCII.GetString(file, at + 4, 4) == type)
                return type == "ftyp" ? (at, at + (int)size) : (at + header, at + (int)size);
            at += (int)size;
        }
        return (null, 0);
    }

    private static (long Size, int Header) BoxSize(byte[] file, int at, int end)
    {
        long size = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(at));
        var header = 8;
        if (size == 1)
        {
            if (at + 16 > end)
                return (0, 0);
            size = (long)BinaryPrimitives.ReadUInt64BigEndian(file.AsSpan(at + 8));
            header = 16;
        }
        else if (size == 0)
            size = end - at;
        return size < header || at + size > end ? (0, 0) : (size, header);
    }

    private static ulong Read(byte[] file, ref int at, int size)
    {
        ulong value = 0;
        for (var i = 0; i < size; i++)
            value = value << 8 | (at + i < file.Length ? file[at + i] : 0u);
        at += size;
        return value;
    }

    private static string ReadString(byte[] file, ref int at, int end)
    {
        var start = at;
        while (at < end && file[at] != 0)
            at++;
        var text = Encoding.UTF8.GetString(file, start, at - start);
        at++;
        return text;
    }
}
