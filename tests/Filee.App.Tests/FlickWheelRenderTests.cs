// fork: the wheel drawn in Tangerine's look (DonutMenu.Look.cs), on the light grey of the recording's desktop.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Filee.App.Controls;
using Filee.App.Services;

namespace Filee.App.Tests;

public class FlickWheelRenderTests
{
    private static readonly Color Desktop = Color.Parse("#F2F2F2");

    [AvaloniaFact]
    public void Formats_wheel_matches_the_recording()
    {
        TestServices.EnsureInitialized("en");
        var (window, donut) = Show(new[] { "MP3", "M4A", "FLAC", "OGG", "OPUS", "AIFF" }.Select(Format).ToList());

        using (var idle = Capture(window, "flick-wheel-formats.png"))
        {
            // Nothing is lit, and the hole shows the desktop.
            Assert.Equal(Desktop, PixelAt(idle, window, donut, Center(donut)));
        }

        donut.SetExternalHighlight(0);
        Settle(donut);
        using var lit = Capture(window, "flick-wheel-formats-mp3.png");
        var orange = PixelAt(lit, window, donut, OnPetal(donut, 0, donut.Layout.Outer - 6));
        Assert.True(orange.R > 230 && orange.G is > 60 and < 110 && orange.B < 40, $"selected petal is {orange}");
        var neighbour = PixelAt(lit, window, donut, OnPetal(donut, 1, donut.Layout.Outer - 6));
        Assert.True(neighbour.R > 200 && Math.Abs(neighbour.R - neighbour.B) < 12, $"other petal is {neighbour}");
        window.Close();
    }

    [AvaloniaFact]
    public void A_tool_draws_its_icon_above_its_label()
    {
        TestServices.EnsureInitialized("en");
        var tools = new[] { "compress", "removeMetadata", "splitPDF", "organizePDF" }
            .Select(id => new DonutItem { Id = id, Label = id, Icon = WheelIcons.Find(id) })
            .ToList();
        var (window, donut) = Show(tools);
        using var frame = Capture(window, "flick-wheel-tools.png");

        // Slice 0 (top): dark icon strokes above the label point, the label below it.
        var s = donut.OuterRadius / PetalLayout.SiteOuter;
        var label = DonutGeometry.PointAt(Center(donut), donut.Layout.LabelRadius, DonutGeometry.MidAngle(0, 4));
        Assert.True(DarkPixels(frame, window, donut, new Rect(label.X - 9 * s, label.Y - 16.5 * s, 18 * s, 18 * s)) > 20);
        Assert.True(DarkPixels(frame, window, donut, new Rect(label.X - 25 * s, label.Y + 6 * s, 50 * s, 9 * s)) > 10);
        window.Close();
    }

    [AvaloniaFact]
    public void Three_slices_start_on_the_left()
    {
        TestServices.EnsureInitialized("en");
        var (window, donut) = Show(new[] { "PDF", "JPG", "PNG" }.Select(Format).ToList());
        donut.SetExternalHighlight(0);
        Settle(donut);
        using var frame = Capture(window, "flick-wheel-three.png");

        var left = PixelAt(frame, window, donut, Center(donut) + new Vector(-donut.Layout.LabelRadius - 20, 0));
        Assert.True(left.R > 230 && left.B < 40, $"left of the hole is {left}");
        window.Close();
    }

    [AvaloniaFact]
    public void An_empty_wheel_shows_a_card_instead_of_the_ring()
    {
        TestServices.EnsureInitialized("en");
        var (window, donut) = Show([]);
        donut.CenterSubtitle = "No editing tools for this format. Tap Shift to convert or Alt for file actions.";
        Settle(donut);
        using var frame = Capture(window, "flick-wheel-empty.png");

        // Inside the card's left padding (245 wide, 24 padding), clear of the text.
        var padding = PixelAt(frame, window, donut, Center(donut) + new Vector(-110, 0));
        Assert.True(padding.R > 250 && padding.G > 250 && padding.B > 250, $"the card is {padding}");
        // No track around it: the desktop shows where the ring would be.
        Assert.Equal(Desktop, PixelAt(frame, window, donut, Center(donut) + new Vector(0, -donut.Layout.Outer)));
        window.Close();
    }

    [AvaloniaFact]
    public void Tools_not_built_yet_are_greyed_and_say_so()
    {
        TestServices.EnsureInitialized("en");
        var vm = AppHost.Get<Filee.App.ViewModels.RadialViewModel>();
        vm.ResetForDrag();
        vm.Load([@"C:\flick\notes.pdf"], clickMode: false);
        Assert.True(vm.ShowWheel(Filee.Core.Settings.WheelKind.Tools));
        var (window, donut) = Show(vm.Items);
        donut.SetExternalHighlight(3); // Organize: coming soon
        Settle(donut);
        using var frame = Capture(window, "flick-wheel-pdf-tools.png");

        // Hovering a greyed-out slice doesn't light it.
        var organize = PixelAt(frame, window, donut, OnPetal(donut, 3, donut.Layout.Outer - 6));
        Assert.True(Math.Abs(organize.R - organize.B) < 16, $"a coming-soon petal is {organize}");
        window.Close();
    }

    private static DonutItem Format(string name) => new() { Id = name, Label = name };

    private static (Window, DonutMenu) Show(IReadOnlyList<DonutItem> items)
    {
        var donut = new DonutMenu { OuterRadius = PetalLayout.SiteOuter, Items = items };
        var window = new Window
        {
            Content = donut, SizeToContent = SizeToContent.WidthAndHeight, Background = new SolidColorBrush(Desktop),
        };
        window.Show();
        Settle(donut);
        return (window, donut);
    }

    private static void Settle(DonutMenu donut)
    {
        donut.FinishAnimations();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static Point Center(DonutMenu donut) => new(donut.Bounds.Width / 2, donut.Bounds.Height / 2);

    private static Point OnPetal(DonutMenu donut, int index, double radius) =>
        DonutGeometry.PointAt(Center(donut), radius, DonutGeometry.MidAngle(index, donut.Items!.Count));

    private static WriteableBitmap Capture(Window window, string name)
    {
        var frame = window.CaptureRenderedFrame()!;
        using (var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, name)))
            frame.Save(stream, new PngBitmapEncoderOptions());
        return frame;
    }

    private static Color PixelAt(WriteableBitmap bitmap, Window window, DonutMenu donut, Point p)
    {
        var at = donut.TranslatePoint(p, window)!.Value;
        using var buffer = bitmap.Lock();
        var offset = (int)at.Y * buffer.RowBytes + (int)at.X * 4;
        var bytes = new byte[4];
        Marshal.Copy(buffer.Address + offset, bytes, 0, 4);
        return buffer.Format == Avalonia.Platform.PixelFormat.Rgba8888
            ? Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2])
            : Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
    }

    private static int DarkPixels(WriteableBitmap bitmap, Window window, DonutMenu donut, Rect area)
    {
        var count = 0;
        for (var y = area.Top; y < area.Bottom; y++)
            for (var x = area.Left; x < area.Right; x++)
            {
                var c = PixelAt(bitmap, window, donut, new Point(x, y));
                if (c.R + c.G + c.B < 300)
                    count++;
            }
        return count;
    }
}
