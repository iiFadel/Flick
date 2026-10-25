// fork: what a video or audio editor window asked for (MediaOptions.Edit, Phase 5), added to MediaEncoding's ordinary
// encode of the file into its own format: Trim seeks the input to the start and stops at the end. FFmpeg seeks a
// decoded input exactly, and an edit never copies streams (MediaEncoding.CanCopyVideo / CanCopyAudio).

using System.Globalization;
using Filee.Core.Presets;

namespace Filee.Engines.Media;

internal static class MediaEdits
{
    /// <summary><paramref name="passes"/> with <paramref name="options"/>' edit applied (as they are without one).</summary>
    public static IReadOnlyList<FfmpegPass> Apply(IReadOnlyList<FfmpegPass> passes, MediaOptions options)
    {
        if (options.Edit?.Trim is not { } trim)
            return passes;
        return [.. passes.Select(pass => pass with { Arguments = BeforeFirstInput(pass.Arguments, ["-ss", Number(trim.Start), "-to", Number(trim.End)]) })];
    }

    /// <summary>How long the output plays (FFmpeg's progress counts the output's time).</summary>
    public static TimeSpan? Duration(TimeSpan? duration, MediaOptions options) =>
        options.Edit?.Trim is { } trim ? TimeSpan.FromSeconds(trim.Length) : duration;

    private static List<string> BeforeFirstInput(IReadOnlyList<string> arguments, IEnumerable<string> added)
    {
        var list = arguments.ToList();
        var at = list.IndexOf("-i");
        list.InsertRange(at < 0 ? 0 : at, added);
        return list;
    }

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
