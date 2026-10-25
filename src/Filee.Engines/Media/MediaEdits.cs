// fork: what a video or audio editor window asked for (MediaOptions.Edit, Phase 5), added to MediaEncoding's ordinary
// encode of the file into its own format: Trim seeks the input to the start and stops at the end; Redact hides boxes on
// every frame; Crop puts a crop before the encode's own video filters, as fractions of the upright picture (FFmpeg turns phone videos before filtering), with
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

    /// <summary>
    /// The redactions, then the crop, then the encode's own <paramref name="rest"/>, as one filter graph for -vf. A
    /// solid box is a filled drawbox; a blurred one is cut out, blurred three times over a quarter of its side, and laid
    /// back on the frame.
    /// </summary>
    internal static string VideoFilters(MediaEdit edit, string rest)
    {
        var chains = new List<string>();
        var current = "";
        for (var i = 0; i < edit.Redactions.Count; i++)
        {
            var b = CropMath.Clamp(edit.Redactions[i]);
            var (x, y, w, h) = (Number(b.X), Number(b.Y), Number(b.Width), Number(b.Height));
            if (edit.RedactStyle == RedactStyle.Solid)
            {
                chains.Add($"{current}drawbox=x='iw*{x}':y='ih*{y}':w='iw*{w}':h='ih*{h}':color=black:t=fill[r{i}]");
            }
            else
            {
                chains.Add($"{current}split=2[base{i}][cut{i}]");
                chains.Add($"[cut{i}]crop=w='max(8,trunc(iw*{w}/2)*2)':h='max(8,trunc(ih*{h}/2)*2)':x='trunc(iw*{x}/2)*2':y='trunc(ih*{y}/2)*2',"
                    + $"boxblur=lr='min(w,h)/4':lp=3:cr='min(cw,ch)/4':cp=3[blur{i}]");
                chains.Add($"[base{i}][blur{i}]overlay=x='trunc(W*{x}/2)*2':y='trunc(H*{y}/2)*2'[r{i}]");
            }
            current = $"[r{i}]";
        }
        var tail = new List<string>();
        if (edit.Crop is { } crop)
            tail.Add(CropFilter(crop));
        if (rest.Length > 0)
            tail.Add(rest);
        chains.Add(current + (tail.Count > 0 ? string.Join(",", tail) : "null"));
        return string.Join(";", chains);
    }

    private static List<string> Edited(IReadOnlyList<string> arguments, MediaEdit edit)
    {
        var list = arguments.ToList();
        if (edit.Crop is not null || edit.Redactions.Count > 0)
        {
            var filters = list.IndexOf("-vf");
            if (filters < 0)
                throw new InvalidOperationException("The video is not re-encoded, so it can't be edited.");
            list[filters + 1] = VideoFilters(edit, list[filters + 1]);
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
