// fork: the Organize editor's pages (Phase 5): the document's pages in a new order, each turned by a quarter turn or
// more, some twice, some left out. Never empty. Pure, unit tested (PageOrganizerTests).

namespace Filee.Core.Editing;

/// <summary>One page of the new document: page <paramref name="Source"/> (0-based) of the original, turned clockwise.</summary>
/// <param name="Rotation">0, 90, 180 or 270 degrees, added to the page's own rotation.</param>
public readonly record struct PagePlacement(int Source, int Rotation = 0);

/// <summary>The pages of the new document, as the Organize editor changes them.</summary>
public sealed class PageOrganizer
{
    private readonly List<PagePlacement> _pages;

    /// <summary>The original order of a document of <paramref name="pageCount"/> pages.</summary>
    public PageOrganizer(int pageCount)
    {
        PageCount = Math.Max(0, pageCount);
        _pages = [.. Enumerable.Range(0, PageCount).Select(i => new PagePlacement(i))];
    }

    /// <summary>Pages in the original document.</summary>
    public int PageCount { get; }

    public IReadOnlyList<PagePlacement> Pages => _pages;

    /// <summary>True while the document would come out as it went in.</summary>
    public bool IsUnchanged => _pages.Count == PageCount && _pages.Select((page, i) => page == new PagePlacement(i)).All(same => same);

    /// <summary>Moves the page at <paramref name="from"/> so it ends up at <paramref name="to"/>.</summary>
    public void Move(int from, int to)
    {
        if (!Valid(from))
            return;
        to = Math.Clamp(to, 0, _pages.Count - 1);
        var page = _pages[from];
        _pages.RemoveAt(from);
        _pages.Insert(to, page);
    }

    /// <summary>Turns the page a quarter turn clockwise (<paramref name="quarters"/> 1) or anticlockwise (−1).</summary>
    public void Rotate(int index, int quarters)
    {
        if (!Valid(index))
            return;
        var page = _pages[index];
        _pages[index] = page with { Rotation = ((page.Rotation + quarters * 90) % 360 + 360) % 360 };
    }

    /// <summary>A copy of the page right after it.</summary>
    public void Duplicate(int index)
    {
        if (Valid(index))
            _pages.Insert(index + 1, _pages[index]);
    }

    /// <summary>Leaves the page out; the last page left stays.</summary>
    /// <returns>True when it was removed.</returns>
    public bool Remove(int index)
    {
        if (!Valid(index) || _pages.Count <= 1)
            return false;
        _pages.RemoveAt(index);
        return true;
    }

    private bool Valid(int index) => index >= 0 && index < _pages.Count;
}
