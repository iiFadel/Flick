// fork: the windows' colours and font follow Tangerine's site (Styles/Flick.axaml, ThemeService.FontFamilies).

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Filee.App.Services;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class FlickThemeTests
{
    [Fact]
    public void On_windows_the_system_font_leads()
    {
        var first = ThemeService.FontFamilies("en").Split(", ")[0];
        Assert.Equal(OperatingSystem.IsWindows() ? "Segoe UI" : "fonts:Filee#Noto Sans", first);
    }

    [AvaloniaTheory]
    [InlineData("Light", "PageBrush", "#F4F4F4")]
    [InlineData("Light", "SurfaceBrush", "#FFFFFF")]
    [InlineData("Light", "SubtleBorderBrush", "#E9ECE8")]
    [InlineData("Light", "TextPrimaryBrush", "#282B29")]
    [InlineData("Light", "TextSecondaryBrush", "#636D64")]
    [InlineData("Dark", "PageBrush", "#1C1D1C")]
    [InlineData("Dark", "TextPrimaryBrush", "#EDEEEB")]
    public void The_windows_use_the_sites_colours(string variant, string key, string color)
    {
        TestServices.EnsureInitialized("en");
        AppHost.Get<ThemeService>().Apply(new ThemeSettings(), "en");
        var theme = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        Assert.True(Application.Current!.TryGetResource(key, theme, out var value));
        Assert.Equal(Color.Parse(color), Assert.IsAssignableFrom<ISolidColorBrush>(value).Color);
    }

    [Fact]
    public void Muted_text_is_readable_on_the_light_cards()
    {
        // WCAG AA asks 4.5:1 for small text; the site's #828782 has about 3.7:1 on white.
        Assert.True(Contrast(Color.Parse("#636D64"), Colors.White) >= 4.5);
        Assert.True(Contrast(Color.Parse("#636D64"), Color.Parse("#F4F4F4")) >= 4.5);
    }

    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte c) => c / 255.0 <= 0.03928 ? c / 255.0 / 12.92 : Math.Pow((c / 255.0 + 0.055) / 1.055, 2.4);
        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        var (l1, l2) = (Luminance(a), Luminance(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
