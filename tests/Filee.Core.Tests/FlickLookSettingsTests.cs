// fork: schema 32 moves untouched look defaults to Tangerine's (orange accent, its wheel size).

using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class FlickLookSettingsTests
{
    [Fact]
    public void Fresh_settings_have_the_orange_accent_and_tangerines_wheel_size()
    {
        var settings = new AppSettings();

        Assert.Equal("#FF5200", settings.Theme.Accent);
        Assert.Equal(120, settings.Donut.OuterRadius);
    }

    [Theory]
    [InlineData("#7F77DD")]
    [InlineData("#7f77dd")]
    public void Version_31_with_the_old_default_accent_turns_orange(string accent)
    {
        var settings = new AppSettings { SchemaVersion = 31, Theme = { Accent = accent } };

        SettingsMigrations.Apply(settings);

        Assert.Equal("#FF5200", settings.Theme.Accent);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Version_31_with_a_chosen_accent_keeps_it()
    {
        var settings = new AppSettings { SchemaVersion = 31, Theme = { Accent = "#378ADD" } };

        SettingsMigrations.Apply(settings);

        Assert.Equal("#378ADD", settings.Theme.Accent);
    }

    [Theory]
    [InlineData(150, 120)]
    [InlineData(180, 180)]
    public void Version_31_wheel_size_changes_only_from_the_old_default(double before, double after)
    {
        var settings = new AppSettings { SchemaVersion = 31, Donut = { OuterRadius = before } };

        SettingsMigrations.Apply(settings);

        Assert.Equal(after, settings.Donut.OuterRadius);
    }
}
