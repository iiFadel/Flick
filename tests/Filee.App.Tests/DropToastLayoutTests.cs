// fork: where the drop toast goes and what it says (docs/TANGERINE-SPEC.md, "After the drop").

using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.Core.Localization;

namespace Filee.App.Tests;

public class DropToastLayoutTests
{
    [Fact]
    public void The_toast_is_centred_on_the_drop()
    {
        Assert.Equal((800, 470), DropToastLayout.Position(1000, 520, 400, 100, 0, 0, 1920, 1040));
    }

    [Theory]
    [InlineData(10, 500, 8, 450)]       // left edge
    [InlineData(1910, 500, 1512, 450)]  // right edge
    [InlineData(1000, 5, 800, 8)]       // top edge
    [InlineData(1000, 1035, 800, 932)]  // bottom edge, above the taskbar
    public void It_stays_inside_the_work_area(int x, int y, int left, int top) =>
        Assert.Equal((left, top), DropToastLayout.Position(x, y, 400, 100, 0, 0, 1920, 1040));

    [Fact]
    public void A_second_screen_keeps_its_own_edges()
    {
        Assert.Equal((1928, 450), DropToastLayout.Position(1925, 500, 400, 100, 1920, 0, 2560, 1400));
    }

    [AvaloniaFact]
    public void While_running_it_shows_the_file_and_the_percentage()
    {
        TestServices.EnsureInitialized("en");
        var loc = AppHost.Get<ILocalizer>();

        Assert.Equal("sample.wav · 42%", DropToastLayout.Status(loc, "sample.wav", 1, 41.6, 0, finished: false, download: null));
        Assert.Equal(loc.Format("home.files", 3) + " · 7%", DropToastLayout.Status(loc, null, 3, 7, 1, finished: false, download: null));
        Assert.Equal("2.4 MB/s · 12.3 / 48.0 MB",
            DropToastLayout.Status(loc, "photo.jpg", 1, 10, 0, finished: false, download: "2.4 MB/s · 12.3 / 48.0 MB"));
    }

    [AvaloniaFact]
    public void When_done_it_counts_what_was_saved()
    {
        TestServices.EnsureInitialized("en");
        var loc = AppHost.Get<ILocalizer>();

        Assert.Equal("1 of 1 saved", DropToastLayout.Status(loc, "sample.wav", 1, 100, 1, finished: true, download: null));
        Assert.Equal("2 of 3 saved", DropToastLayout.Status(loc, null, 3, 100, 2, finished: true, download: null));
    }

    [AvaloniaFact]
    public void The_title_says_what_the_slice_does()
    {
        TestServices.EnsureInitialized("en");
        var loc = AppHost.Get<ILocalizer>();

        Assert.Equal("Converting to MP3", DropToastLayout.Title(loc, "MP3", conversion: true));
        Assert.Equal("Split", DropToastLayout.Title(loc, "Split", conversion: false));
    }
}
