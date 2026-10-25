// fork: what a video or audio editor window asked for (MediaOptions.Edit, Phase 5), added to MediaEncoding's ordinary
// encode of the file into its own format: Trim seeks the input to the start and stops at the end; Crop puts a crop
// first in the video filters, as fractions of the upright picture (FFmpeg turns phone videos before filtering), with
// even sides for H.264 and HEVC. FFmpeg seeks a decoded input exactly, and an edit never copies streams
// (MediaEncoding.CanCopyVideo / CanCopyAudio).

using System.Globalization;
using Filee.Core.Editing;
using Filee.Core.Presets;

namespace Filee.Engines.Media;

internal static class MediaEdits
{
    /// <summary><paramref name="passes"/> with <paramref name="options"/>' edit applied (as they are without one).</summary>
    public static IReadOnlyList<FfmpegPass> Apply(IReadOnlyList<FfmpegPass> passes, MediaOptions options)
    {
        if (options.Edit is not { } edit)
            return passes;
        return [.. passes.Select(pass => pass with { Arguments = Edited(pass.Arguments, edit) })];
    }

    /// <summary>How long the output plays (FFmpeg's progress counts the output's time).</summary>
    public static TimeSpan? Duration(TimeSpan? duration, MediaOptions options) =>
        options.Edit?.Trim is { } trim ? TimeSpan.FromSeconds(trim.Length) : duration;

    /// <summary>FFmpeg's crop filter for <paramref name="crop"/>: input-relative, every side rounded down to even.</summary>
    internal static string CropFilter(NormalizedRect crop)
    {
        var c = CropMath.Clamp(crop);
        return $"crop=w='max(2,trunc(iw*{Number(c.Width)}/2)*2)':h='max(2,trunc(ih*{Number(c.Height)}/2)*2)'"
            + $":x='trunc(iw*{Number(c.X)}/2)*2':y='trunc(ih*{Number(c.Y)}/2)*2'";
    }

    private static List<string> Edited(IReadOnlyList<string> arguments, MediaEdit edit)
    {
        var list = arguments.ToList();
        if (edit.Crop is { } crop)
        {
            var filters = list.IndexOf("-vf");
            if (filters < 0)
                throw new InvalidOperationException("The video is not re-encoded, so it can't be cropped.");
            list[filters + 1] = CropFilter(crop) + (list[filters + 1].Length > 0 ? "," + list[filters + 1] : "");
        }
        if (edit.Trim is { } trim)
        {
            var at = list.IndexOf("-i");
            list.InsertRange(at < 0 ? 0 : at, ["-ss", Number(trim.Start), "-to", Number(trim.End)]);
        }
        return list;
    }

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
