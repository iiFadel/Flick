// fork: a GIF without its metadata, the frames copied as they are. Kept extensions: frame timing (graphic control),
// plain text (drawn on the picture) and the looping ones (NETSCAPE2.0, ANIMEXTS1.0), which an animation needs.
// Comments, the other application extensions (XMP, ICC and maker data live there) and unknown ones go.

namespace Filee.Engines.Metadata;

internal static class GifMetadata
{
    /// <returns>The file without metadata, or null when it is not a GIF this can follow.</returns>
    public static byte[]? Strip(byte[] gif)
    {
        if (gif.Length < 13 || !(gif.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || gif.AsSpan(0, 6).SequenceEqual("GIF89a"u8)))
            return null;
        var at = 13;
        if ((gif[10] & 0x80) != 0)
            at += 3 << ((gif[10] & 7) + 1); // global colour table
        using var output = new MemoryStream(gif.Length);
        output.Write(gif, 0, at);
        while (at < gif.Length)
        {
            var start = at;
            switch (gif[at])
            {
                case 0x3B: // trailer
                    output.WriteByte(0x3B);
                    return output.ToArray();
                case 0x21 when at + 2 < gif.Length:
                {
                    var label = gif[at + 1];
                    at += 2;
                    var first = at;
                    if (SkipSubBlocks(gif, ref at) is false)
                        return null;
                    if (label is 0xF9 or 0x01 || label == 0xFF && Loops(gif, first))
                        output.Write(gif, start, at - start);
                    break;
                }
                case 0x2C when at + 10 <= gif.Length: // image: descriptor, local colour table, LZW minimum code size, data
                {
                    var packed = gif[at + 9];
                    at += 10;
                    if ((packed & 0x80) != 0)
                        at += 3 << ((packed & 7) + 1);
                    at++;
                    if (at > gif.Length || SkipSubBlocks(gif, ref at) is false)
                        return null;
                    output.Write(gif, start, at - start);
                    break;
                }
                default:
                    return null;
            }
        }
        return null;
    }

    private static bool Loops(byte[] gif, int block) =>
        block + 12 <= gif.Length && gif[block] == 11
        && (gif.AsSpan(block + 1, 11).SequenceEqual("NETSCAPE2.0"u8) || gif.AsSpan(block + 1, 11).SequenceEqual("ANIMEXTS1.0"u8));

    private static bool SkipSubBlocks(byte[] gif, ref int at)
    {
        while (at < gif.Length)
        {
            var size = gif[at];
            at += 1 + size;
            if (size == 0)
                return at <= gif.Length;
        }
        return false;
    }
}
