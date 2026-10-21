// fork: Tangerine's wheel timing, and the wheel following it.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Filee.App.Controls;
using Filee.App.Services;

namespace Filee.App.Tests;

public class WheelMotionTests
{
    [Theory]
    // Values of cubic-bezier(.18, .82, .24, 1) worked out separately (bisection to full precision).
    [InlineData(0.1, 0.418314)]
    [InlineData(0.2, 0.686226)]
    [InlineData(0.25, 0.767328)]
    [InlineData(0.5, 0.944901)]
    [InlineData(0.75, 0.990991)]
    [InlineData(0.9, 0.998826)]
    public void The_bloom_follows_the_sites_curve(double t, double expected) =>
        Assert.Equal(expected, WheelMotion.Open(t), 5);

    [Fact]
    public void The_bloom_starts_at_zero_ends_at_one_and_never_goes_back()
    {
        Assert.Equal(0, WheelMotion.Open(0));
        Assert.Equal(1, WheelMotion.Open(1));
        Assert.Equal(1, WheelMotion.Open(2));
        for (var t = 0.0; t < 1; t += 0.01)
            Assert.True(WheelMotion.Open(t + 0.01) >= WheelMotion.Open(t));
    }

    [Fact]
    public void Labels_fade_in_after_forty_milliseconds()
    {
        Assert.Equal(0, WheelMotion.LabelAlpha(0.03));
        Assert.Equal(0, WheelMotion.LabelAlpha(0.04));
        Assert.Equal(0.5, WheelMotion.LabelAlpha(0.08), 6);
        Assert.Equal(1, WheelMotion.LabelAlpha(0.12), 9);
    }

    [AvaloniaFact]
    public void The_wheel_opens_in_two_hundred_milliseconds()
    {
        var (donut, window) = Show();
        try
        {
            Motion.Configure(false, () => false);
            donut.PlayOpenAnimation();
            Assert.Equal(0, donut.WheelOpen, 6);
            Assert.Equal(0, donut.LabelOpacity, 6);

            donut.Advance(0.03);
            Assert.Equal(0, donut.LabelOpacity, 6); // labels wait for the wheel to grow
            Assert.InRange(donut.WheelOpen, 0.3, 0.9);

            donut.Advance(0.07);
            Assert.InRange(donut.WheelOpen, 0.9, 1);
            Assert.InRange(donut.LabelOpacity, 0.5, 1);

            donut.Advance(0.1);
            Assert.Equal(1, donut.WheelOpen, 6);
            Assert.Equal(1, donut.LabelOpacity, 6);
            Assert.False(donut.Advance(0.016)); // done: no more frames
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_petal_lights_up_in_a_hundred_and_twenty_milliseconds_without_bouncing()
    {
        var (donut, window) = Show();
        try
        {
            Motion.Configure(false, () => false);
            donut.FinishAnimations();
            donut.SetExternalHighlight(1);
            donut.Advance(0.06);
            Assert.Equal(0.5, donut.LitAmount(1), 2);
            donut.Advance(0.07);
            Assert.Equal(1, donut.LitAmount(1), 6);

            donut.SetExternalHighlight(2);
            for (var i = 0; i < 10; i++)
            {
                donut.Advance(0.016);
                Assert.InRange(donut.LitAmount(1), 0, 1);
                Assert.InRange(donut.LitAmount(2), 0, 1);
            }
            Assert.Equal(0, donut.LitAmount(1), 6);
            Assert.Equal(1, donut.LitAmount(2), 6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduce_animations_opens_and_lights_at_once()
    {
        var (donut, window) = Show();
        try
        {
            Motion.Configure(true, () => false);
            donut.PlayOpenAnimation();
            Assert.Equal(1, donut.WheelOpen, 6);
            Assert.Equal(1, donut.LabelOpacity, 6);
            donut.SetExternalHighlight(3);
            Assert.False(donut.Advance(0));
            Assert.Equal(1, donut.LitAmount(3), 6);
        }
        finally
        {
            Motion.Configure(false, () => false);
            window.Close();
        }
    }

    private static (DonutMenu, Window) Show()
    {
        TestServices.EnsureInitialized("en");
        var donut = new DonutMenu
        {
            Items = new[] { "MP3", "M4A", "FLAC", "OGG", "OPUS", "AIFF" }.Select(l => new DonutItem { Id = l, Label = l }).ToList(),
        };
        var window = new Window { Content = donut, SizeToContent = SizeToContent.WidthAndHeight };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (donut, window);
    }
}
