// fork: what an image editor window (Phase 5) asks the image engine to do, on ImageOptions.Edit. Built from the window
// when Apply is pressed, never saved.

namespace Filee.Core.Editing;

public sealed record ImageEdit
{
    /// <summary>The part of the upright picture (after its EXIF turn) to keep, or null for all of it.</summary>
    public NormalizedRect? Crop { get; init; }

    /// <summary>Boxes to hide, as fractions of the upright picture.</summary>
    public IReadOnlyList<NormalizedRect> Redactions { get; init; } = [];

    public RedactStyle RedactStyle { get; init; }

    /// <summary>Marks to draw on the upright picture (Annotate), with the number each callout shows.</summary>
    public IReadOnlyList<(Annotation Mark, int Number)> Annotations { get; init; } = [];
}
