// fork: the Tools wheel's Compress never leaves a file larger than the one dropped (OutputRule.NeverLarger): a
// result of the source's own format that didn't get smaller is replaced by a copy of the source, as Compress PDF
// does inside its engine.

namespace Filee.Core.Conversion;

internal static class NeverLarger
{
    public static void Apply(FileResult file, IReadOnlyList<string> outputs)
    {
        if (outputs.Count != 1 || !File.Exists(outputs[0]) || !File.Exists(file.SourcePath))
            return;
        var output = outputs[0];
        var sameFormat = string.Equals(Path.GetExtension(output), Path.GetExtension(file.SourcePath), StringComparison.OrdinalIgnoreCase);
        if (new FileInfo(output).Length < new FileInfo(file.SourcePath).Length)
            return;
        // A WAV that became a larger FLAC can't be copied under the FLAC's name: it keeps the result and the notice.
        if (sameFormat)
            File.Copy(file.SourcePath, output, overwrite: true);
        file.NoticeKey ??= "notice.not_smaller";
    }
}
