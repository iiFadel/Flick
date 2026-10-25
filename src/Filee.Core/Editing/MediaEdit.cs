// fork: what a video or audio editor window (Phase 5) asks the FFmpeg engine to do, on MediaOptions.Edit. Built from
// the window when Apply is pressed, never saved. Any edit re-encodes: a stream copy could only cut at keyframes.

namespace Filee.Core.Editing;

public sealed record MediaEdit
{
    /// <summary>The part to keep, in seconds from the start, or null for all of it.</summary>
    public TrimRange? Trim { get; init; }
}
