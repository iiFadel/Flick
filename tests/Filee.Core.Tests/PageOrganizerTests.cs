// fork: the Organize editor's pages (Core/Editing/PageOrganizer.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class PageOrganizerTests
{
    private static int[] Order(PageOrganizer pages) => [.. pages.Pages.Select(p => p.Source)];

    [Fact]
    public void A_new_organizer_is_the_document_as_it_is()
    {
        var pages = new PageOrganizer(3);

        Assert.Equal([0, 1, 2], Order(pages));
        Assert.True(pages.IsUnchanged);
    }

    [Fact]
    public void Move_puts_the_page_where_it_was_dropped()
    {
        var pages = new PageOrganizer(4);

        pages.Move(0, 2);
        Assert.Equal([1, 2, 0, 3], Order(pages));
        pages.Move(3, 0);
        Assert.Equal([3, 1, 2, 0], Order(pages));
        pages.Move(1, 99); // past the end: last
        Assert.Equal([3, 2, 0, 1], Order(pages));
        pages.Move(-1, 0); // no such page: nothing
        Assert.Equal([3, 2, 0, 1], Order(pages));
        Assert.False(pages.IsUnchanged);
    }

    [Fact]
    public void Rotate_turns_by_quarters_both_ways()
    {
        var pages = new PageOrganizer(1);

        pages.Rotate(0, -1);
        Assert.Equal(270, pages.Pages[0].Rotation);
        pages.Rotate(0, 2);
        Assert.Equal(90, pages.Pages[0].Rotation);
        pages.Rotate(0, 3);
        Assert.Equal(0, pages.Pages[0].Rotation);
        Assert.True(pages.IsUnchanged); // a full turn is no change
    }

    [Fact]
    public void Duplicate_copies_the_page_and_its_turn_right_after_it()
    {
        var pages = new PageOrganizer(2);
        pages.Rotate(0, 1);

        pages.Duplicate(0);

        Assert.Equal([new PagePlacement(0, 90), new PagePlacement(0, 90), new PagePlacement(1)], pages.Pages);
    }

    [Fact]
    public void The_last_page_left_can_not_be_removed()
    {
        var pages = new PageOrganizer(2);

        Assert.True(pages.Remove(0));
        Assert.Equal([1], Order(pages));
        Assert.False(pages.Remove(0));
        Assert.Equal([1], Order(pages));
        Assert.False(pages.IsUnchanged);
        Assert.False(new PageOrganizer(0).Remove(0));
    }
}
