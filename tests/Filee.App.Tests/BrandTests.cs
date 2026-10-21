using System.Text.Json;
using Filee.App.Services;

namespace Filee.App.Tests;

public class BrandTests
{
    [Theory]
    [InlineData("Filee", "Flick")]
    [InlineData("Keep Filee running?", "Keep Flick running?")]
    [InlineData("allow Filee.\nIt works", "allow Flick.\nIt works")]
    [InlineData("Filee's Dock icon", "Flick's Dock icon")]
    [InlineData("“Convert with Filee”", "“Convert with Flick”")]
    [InlineData("トレイからFileeを終了", "トレイからFlickを終了")]
    [InlineData("Filee: hold {0} and drag files", "Flick: hold {0} and drag files")]
    public void The_upstream_name_reads_flick(string text, string expected) =>
        Assert.Equal(expected, Brand.Apply(text));

    [Theory]
    [InlineData("Run Filee.exe again")]
    [InlineData("https://github.com/KnifeLemon/Filee/issues")]
    [InlineData("FileeRelease")]
    [InlineData("Filees")]
    public void File_names_and_links_keep_it(string text) =>
        Assert.Equal(text, Brand.Apply(text));

    [Fact]
    public void Strings_load_with_the_flick_name()
    {
        Assert.Equal("Flick", LocalizationService.EarlyText("app.name"));
        Assert.DoesNotContain("Filee", LocalizationService.EarlyText("startup.no_answer"));
    }

    public static TheoryData<string> Languages => new() { "en", "es", "ja", "ko", "zh-CN", "pl", "hi", "ru" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void No_shipped_string_says_filee(string code)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Filee.slnx")))
            dir = dir.Parent;
        var file = Path.Combine(dir.FullName, "src", "Filee.App", "Assets", "i18n", code + ".json");
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))!;

        Assert.Empty(strings.Where(s => Brand.Apply(s.Value).Replace("/Filee", "").Contains("Filee")).Select(s => s.Key));
    }
}
