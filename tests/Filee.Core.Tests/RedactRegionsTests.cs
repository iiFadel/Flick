// fork: the Redact editor's boxes (Core/Editing/RedactRegions.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class RedactRegionsTests
{
    [Fact]
    public void A_box_is_drawn_from_either_corner_and_kept_inside_the_picture()
    {
        Assert.Equal(new NormalizedRect(0.2, 0.1, 0.3, 0.4), RedactRegions.Box(0.5, 0.5, 0.2, 0.1));
        Assert.Equal(new NormalizedRect(0, 0.5, 1, 0.5), RedactRegions.Box(-0.3, 0.5, 1.4, 1.2));
    }

    [Fact]
    public void A_tiny_drag_is_a_click_and_draws_nothing()
    {
        var regions = new RedactRegions();

        Assert.Equal(-1, regions.Add(0.5, 0.5, 0.505, 0.9));
        Assert.Empty(regions.Boxes);
        Assert.Null(RedactRegions.Box(0.5, 0.5, 0.5, 0.5));
    }

    [Fact]
    public void A_click_picks_the_box_drawn_last_where_they_overlap()
    {
        var regions = new RedactRegions();
        regions.Add(0, 0, 0.5, 0.5);
        regions.Add(0.25, 0.25, 0.75, 0.75);

        Assert.Equal(1, regions.At(0.3, 0.3));
        Assert.Equal(0, regions.At(0.1, 0.1));
        Assert.Equal(-1, regions.At(0.9, 0.9));
    }

    [Fact]
    public void Undo_takes_back_the_last_box_and_stops_when_none_are_left()
    {
        var regions = new RedactRegions();
        regions.Add(0, 0, 0.5, 0.5);
        regions.Add(0.5, 0.5, 1, 1);

        Assert.True(regions.Undo());
        Assert.Equal([new NormalizedRect(0, 0, 0.5, 0.5)], regions.Boxes);
        Assert.True(regions.Undo());
        Assert.False(regions.Undo());
        Assert.False(regions.Remove(3));
    }
}
