// Keeps the installer script (installer/Filee.iss) and the app in step: the options Setup starts Filee with, the
// Explorer menu package it registers and the engine folders it looks into.

using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.Core.Localization;
using Filee.Engines.Infrastructure;
using Filee.Platform.Windows;

namespace Filee.App.Tests;

public class InstallerTests
{
    private static string Script
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(dir, "Filee.slnx")))
                dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Repository root not found.");
            return File.ReadAllText(Path.Combine(dir, "installer", "Filee.iss"));
        }
    }

    [Fact]
    public void Script_passes_only_options_the_app_knows()
    {
        // Each option below is checked by a parse test; a new one in the script must be added here.
        var used = Regex.Matches(Script, @"--[a-z][a-z-]+=?").Select(m => m.Value).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "--background", "--quit", "--uninstall-cleanup", "--install-engines=", "--start-with-windows=", "--context-menu=" },
            used);
    }

    [Fact]
    public void Installer_choices_are_parsed()
    {
        var options = CommandLine.Parse(["--install-engines=pandoc, FFmpeg,pandoc", "--start-with-windows=off", "--context-menu=on"]);
        Assert.Equal(["pandoc", "ffmpeg"], options.InstallEngines);
        Assert.False(options.StartWithWindows);
        Assert.True(options.ContextMenu);
        Assert.False(options.Background);
        Assert.Empty(options.ConvertFiles);

        // Picking no engine is an answer too (the first-run question is skipped), unlike not passing the option.
        Assert.Empty(CommandLine.Parse(["--install-engines="]).InstallEngines!);
        Assert.Null(CommandLine.Parse(["--background"]).InstallEngines);
        Assert.Null(CommandLine.Parse(["--start-with-windows=maybe"]).StartWithWindows);
        Assert.Null(CommandLine.Parse([]).ContextMenu);
        Assert.True(CommandLine.Parse(["--start-with-system=on"]).StartWithWindows);
    }

    [Fact]
    public void Quit_and_cleanup_are_recognised()
    {
        Assert.True(CommandLine.Parse(["--quit"]).Quit);
        Assert.True(CommandLine.Parse(["--Uninstall-Cleanup"]).UninstallCleanup);
        Assert.False(CommandLine.Parse(["--background"]).Quit);
    }

    [AvaloniaFact]
    public void Engines_from_the_installer_start_only_when_something_is_left_to_install()
    {
        TestServices.EnsureInitialized("en");
        var vm = new EngineSetupViewModel(AppHost.Get<EngineDownloadService>(), AppHost.Get<ILocalizer>());
        Assert.False(vm.InstallNow(["no-such-engine"]));
        Assert.False(vm.InstallNow([]));
        Assert.False(vm.Started);
    }

    [Fact]
    public void Script_uses_the_explorer_menu_package_identity()
    {
        var script = Script;
        Assert.Contains($"ExplorerMenuName = '{ExplorerMenuPackage.PackageName}';", script);
        Assert.Contains($"ExplorerMenuFamily = '{ExplorerMenuPackage.FamilyName}';", script);
        Assert.Contains($"ExplorerMenuDll = '{ExplorerMenuPackage.DllFileName}';", script);
        Assert.Contains($"ExplorerMenuPackageFile = '{ExplorerMenuPackage.PackageFileName}';", script);
        Assert.Contains($"ExplorerMenuMinimumBuild = {ExplorerMenuPackage.MinimumBuild};", script);
    }

    [Fact]
    public void Setup_shows_Flick_and_keeps_the_upstream_files_and_id()
    {
        var script = Script;
        Assert.Contains("#define AppName \"Flick\"", script);
        Assert.Contains("#define AppExe \"Filee.exe\"", script);
        Assert.Contains("#define AppGuid \"C8686E06-8327-40A2-B0F7-3DEDD61D9248\"", script); // updates find the install
        Assert.Contains(@"Name: ""{autoprograms}\Filee.lnk""", script); // the old Start menu shortcut goes
        // Every message Setup shows says Flick; "filee" (the command) and Filee.exe are names of files.
        var messages = Regex.Matches(script, @"^(en|ko|zhcn|ja)\.\w+=(.*)$", RegexOptions.Multiline).Select(m => m.Groups[2].Value);
        Assert.All(messages, text => Assert.DoesNotContain("Filee", text, StringComparison.Ordinal));
    }

    [Fact]
    public void Script_finds_downloaded_engines_where_the_app_puts_them()
    {
        var marker = typeof(EngineInstaller).GetField("MarkerName", BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue();
        Assert.Contains($"EngineMarker = '{marker}';", Script);
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Filee", "engines"),
            EngineEnvironment.DownloadRoot);
        Assert.Contains(@"ExpandConstant('{localappdata}\Filee\engines\')", Script);
    }
}
