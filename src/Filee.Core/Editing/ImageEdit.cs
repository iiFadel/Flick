// fork: what an image editor window (Phase 5) asks the image engine to do, on ImageOptions.Edit. Built from the window
// when Apply is pressed, never saved.

namespace Filee.Core.Editing;

public sealed record ImageEdit
{
    /// <summary>The part of the upright picture (after its EXIF turn) to keep, or null for all of it.</summary>
    public NormalizedRect? Crop { get; init; }
}
