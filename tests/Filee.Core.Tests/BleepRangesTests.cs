// fork: the Bleep editor's ranges (Core/Editing/BleepRanges.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class BleepRangesTests
{
    [Fact]
    public void Ranges_are_kept_in_order_inside_the_song()
    {
        var ranges = new BleepRanges(10);

        Assert.Equal(0, ranges.Add(6, 5));
        Assert.Equal(0, ranges.Add(-2, 1));
        Assert.Equal(2, ranges.Add(9, 15));

        Assert.Equal([new TrimRange(0, 1), new TrimRange(5, 6), new TrimRange(9, 10)], ranges.Ranges);
    }

    [Fact]
    public void Overlapping_and_touching_ranges_become_one()
    {
        var ranges = new BleepRanges(10);
        ranges.Add(1, 2);
        ranges.Add(3, 4);
        ranges.Add(6, 7);

        Assert.Equal(0, ranges.Add(1.5, 3.2)); // joins the first two
        Assert.Equal(1, ranges.Add(7, 8));     // touches the last
        Assert.Equal([new TrimRange(1, 4), new TrimRange(6, 8)], ranges.Ranges);
    }

    [Fact]
    public void A_click_is_not_a_range_and_picks_the_one_under_it()
    {
        var ranges = new BleepRanges(10);

        Assert.Equal(-1, ranges.Add(2, 2.01));
        ranges.Add(2, 3);
        Assert.Equal(0, ranges.At(2.5));
        Assert.Equal(-1, ranges.At(5));
        Assert.True(ranges.Remove(0));
        Assert.False(ranges.Remove(0));
        Assert.Equal(-1, new BleepRanges(double.NaN).Add(0, 1)); // no length: nothing to bleep
    }
}
