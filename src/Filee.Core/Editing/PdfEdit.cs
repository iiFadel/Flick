// fork: what the Organize editor window (Phase 5) asks the PDF engine to do, on PdfOptions.Edit. Built from the window
// when Apply is pressed, never saved.

namespace Filee.Core.Editing;

public sealed record PdfEdit
{
    /// <summary>The pages of the new document, in order (never empty).</summary>
    public IReadOnlyList<PagePlacement> Pages { get; init; } = [];
}
