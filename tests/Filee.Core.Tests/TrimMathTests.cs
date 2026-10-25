// fork: the Trim editor's timeline (Core/Editing/TrimMath.cs).

using Filee.Core.Editing;

namespace Filee.Core.Tests;

public class TrimMathTests
{
    [Fact]
    public void Clamp_keeps_the_range_inside_the_recording_and_never_too_short()
    {
        Assert.Equal(new TrimRange(0, 10), TrimMath.Clamp(new TrimRange(-3, 25), 10));
        Assert.Equal(new TrimRange(4, 4.1), TrimMath.Clamp(new TrimRange(4, 4), 10));
        Assert.Equal(new TrimRange(9.9, 10), TrimMath.Clamp(new TrimRange(10, 10), 10));
        Assert.Equal(new TrimRange(0, 10), TrimMath.Clamp(new TrimRange(double.NaN, double.NaN), 10));
        Assert.Equal(new TrimRange(0, 0.05), TrimMath.Clamp(new TrimRange(0, 1), 0.05)); // shorter than the minimum: all of it
    }

    [Fact]
    public void A_handle_stops_short_of_the_other()
    {
        var range = new TrimRange(2, 6);

        Assert.Equal(new TrimRange(6 - TrimMath.MinLength, 6), TrimMath.MoveStart(range, 8, 10));
        Assert.Equal(new TrimRange(2, 2 + TrimMath.MinLength), TrimMath.MoveEnd(range, 1, 10));
        Assert.Equal(new TrimRange(0, 6), TrimMath.MoveStart(range, -5, 10));
        Assert.Equal(new TrimRange(2, 10), TrimMath.MoveEnd(range, 50, 10));
    }

    [Theory]
    [InlineData(25, 0.04)]
    [InlineData(0, 0.1)]
    [InlineData(double.NaN, 0.1)]
    public void A_step_is_one_frame_or_a_tenth_of_a_second(double rate, double step) =>
        Assert.Equal(step, TrimMath.Step(rate), 9);

    [Fact]
    public void Whole_is_the_full_length()
    {
        Assert.True(TrimMath.IsWhole(new TrimRange(0, 10), 10));
        Assert.False(TrimMath.IsWhole(new TrimRange(0, 9.9), 10));
        Assert.False(TrimMath.IsWhole(new TrimRange(0.1, 10), 10));
    }

    [Fact]
    public void Silence_at_both_ends_goes_and_silence_in_the_middle_stays()
    {
        (double, double)[] silences = [(0, 1.2), (3, 4), (8.5, 10)];

        Assert.Equal(new TrimRange(1.2, 8.5), TrimMath.WithoutSilentEnds(silences, 10));
        Assert.Equal(new TrimRange(0, 10), TrimMath.WithoutSilentEnds([(3, 4)], 10));
        Assert.Equal(new TrimRange(0, 10), TrimMath.WithoutSilentEnds([(0, 10)], 10)); // all silence: kept whole
        Assert.Equal(new TrimRange(2, 10), TrimMath.WithoutSilentEnds([(0, 1), (1.02, 2)], 10)); // back to back
    }

    [Theory]
    [InlineData(0, "0:00.0")]
    [InlineData(62.54, "1:02.5")]
    [InlineData(3602.5, "1:00:02.5")]
    [InlineData(-1, "0:00.0")]
    public void Times_read_as_minutes_seconds_and_tenths(double seconds, string text) =>
        Assert.Equal(text, TrimMath.Format(seconds));
}
