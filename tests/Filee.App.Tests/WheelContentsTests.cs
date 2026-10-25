// fork: each category's drag wheels show exactly docs/TANGERINE-SPEC.md's lists, in order ("Wheel contents").

using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.Core.Localization;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class WheelContentsTests
{
    // File, Convert wheel (Shift), Tools wheel (Ctrl; none = Ctrl does nothing), tools already built (not "coming soon").
    public static TheoryData<string, string[], string[], string[]> Categories => new()
    {
        { "photo.jpg", ["PNG", "WEBP", "HEIC", "TIFF", "AVIF", "BMP", "PDF"],
            ["Compress", "Metadata", "Edit", "Annotate", "Add BG", "Crop", "Redact"], ["Compress", "Metadata", "Edit", "Annotate", "Add BG", "Crop", "Redact"] },
        { "clip.mov", ["MP4", "MKV", "WEBM", "AVI", "WMV", "GIF", "MP3"],
            ["Compress", "Metadata", "Mute", "Trim", "Crop", "Speed", "Snapshot", "Split", "Redact"],
            ["Compress", "Metadata", "Mute", "Trim", "Crop", "Speed", "Snapshot", "Split", "Redact"] },
        { "song.mp3", ["M4A", "WAV", "FLAC", "OGG", "OPUS", "AIFF", "WMA"],
            ["Compress", "Metadata", "Normalize", "Visualize", "Trim", "Channels", "Bleep"], ["Compress", "Metadata", "Normalize", "Visualize", "Trim", "Channels", "Bleep"] },
        { "notes.pdf", ["DOCX", "PAGES", "PPTX", "XLSX", "JPG", "PNG", "EPUB", "TXT", "MD"],
            ["Compress", "Metadata", "Split", "Organize"], ["Compress", "Metadata", "Split", "Organize"] },
        { "letter.docx", ["PAGES", "PDF", "TXT", "MD", "HTML", "EPUB", "PNG", "JPG"], [], [] },
        { "slides.pptx", ["PDF", "JPG", "PNG"], [], [] },
        { "budget.xlsx", ["ODS", "CSV", "TSV", "PDF"], [], [] },
        { "book.epub", ["PDF", "DOCX", "PAGES", "TXT", "MD", "HTML"], [], [] },
        { "captions.srt", ["VTT", "TXT"], [], [] },
        { "project.zip", ["TAR", "GZIP"], ["Extract"], ["Extract"] },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Categories))]
    public void Each_category_shows_the_specs_wheels(string file, string[] convert, string[] tools, string[] built)
    {
        TestServices.EnsureInitialized("en");
        var loc = AppHost.Get<ILocalizer>();
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        vm.Load([Path.Combine(@"C:\flick", file)], clickMode: false);

        Assert.True(vm.ShowWheel(WheelKind.Convert));
        Assert.Equal(convert, vm.Items.Select(i => i.Label));
        Assert.All(vm.Items, item => Assert.Null(item.Icon)); // formats are names, never tools

        var opened = vm.ShowWheel(WheelKind.Tools);
        if (tools.Length == 0)
        {
            // No Tools wheel: Ctrl does nothing, the Convert wheel stays.
            Assert.False(opened);
            Assert.Equal(convert, vm.Items.Select(i => i.Label));
            return;
        }

        Assert.True(opened);
        Assert.Equal(tools, vm.Items.Select(i => i.Label));
        Assert.All(vm.Items, item => Assert.NotNull(item.Icon));
        foreach (var item in vm.Items)
        {
            if (built.Contains(item.Label))
                Assert.NotEqual(loc["donut.tool_coming_soon"], item.DisabledReason);
            else
            {
                Assert.False(item.IsEnabled);
                Assert.Equal(loc["donut.tool_coming_soon"], item.DisabledReason);
            }
        }
    }

    [AvaloniaFact]
    public void A_tool_that_is_not_built_never_converts()
    {
        TestServices.EnsureInitialized("en");
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        vm.Load([@"C:\flick\clip.mov"], clickMode: false);

        Assert.True(vm.ShowWheel(WheelKind.Tools));
        var loc = AppHost.Get<ILocalizer>();
        for (var i = 0; i < vm.Items.Count; i++)
        {
            if (vm.Items[i].DisabledReason == loc["donut.tool_coming_soon"])
                Assert.Null(vm.PresetAt(i, -1));
        }
    }

    [AvaloniaFact]
    public void A_tool_operation_runs_on_the_file_as_a_same_format_preset()
    {
        TestServices.EnsureInitialized("en");
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        vm.Load([@"C:\flick\photo.jpg"], clickMode: false);

        Assert.True(vm.ShowWheel(WheelKind.Tools));
        var metadata = vm.Items.Select(i => i.Label).ToList().IndexOf("Metadata");
        var preset = vm.PresetAt(metadata, -1);
        Assert.NotNull(preset);
        Assert.Equal("op-removeMetadata", preset.Id);
        Assert.Equal("Metadata", preset.Name);
        Assert.Equal("{name} No Metadata", preset.Output.FileNamePattern);
        Assert.False(preset.Image.KeepMetadata);
    }

    [AvaloniaFact]
    public void A_folder_among_the_files_leaves_the_wheels_to_the_files()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-folder-");
        try
        {
            var vm = AppHost.Get<RadialViewModel>();
            vm.ResetForDrag();
            vm.Load([@"C:\flick\a.png", folder.FullName], clickMode: false);
            Assert.True(vm.ShowWheel(WheelKind.Convert));
            Assert.Equal(["JPG", "WEBP", "HEIC", "TIFF", "AVIF", "BMP", "PDF"], vm.Items.Select(i => i.Label));
        }
        finally
        {
            folder.Delete();
        }
    }

    [AvaloniaFact]
    public void Several_photos_get_the_batch_tools()
    {
        TestServices.EnsureInitialized("en");
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        vm.Load([@"C:\flick\a.jpg", @"C:\flick\b.png"], clickMode: false);

        Assert.True(vm.ShowWheel(WheelKind.Convert));
        Assert.Equal(["WEBP", "HEIC", "TIFF", "AVIF", "BMP", "PDF"], vm.Items.Select(i => i.Label));
        Assert.True(vm.ShowWheel(WheelKind.Tools));
        Assert.Equal(["Compress", "Metadata", "Crop", "Create PDF", "Collage"], vm.Items.Select(i => i.Label));
    }
}
