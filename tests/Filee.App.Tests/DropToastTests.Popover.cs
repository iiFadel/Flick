// fork: an operation that asks for a value (Channels: Mono or Stereo) shows the value popover where the drop landed;
// a pick starts the job and the drop toast follows, Escape runs nothing. Channels needs FFmpeg to be offered.

using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Raw;
using Filee.App.Services;
using Filee.Core.Conversion;
using Filee.Engines.Infrastructure;
using SharpHook.Data;

namespace Filee.App.Tests;

public partial class DropToastTests
{
    /// <summary>The app's FFmpeg (ffmpeg.exe), or null when it is not installed.</summary>
    private static string? Ffmpeg
    {
        get
        {
            var catalog = AppHost.Get<ConverterCatalog>();
            var status = catalog.StatusOf(AppHost.Get<IReadOnlyList<IConverter>>().Single(c => c.Id == "ffmpeg"));
            return status.IsAvailable ? status.Detail : null;
        }
    }

    /// <summary>A short MP3 made by FFmpeg, its Tools wheel open; returns the CHANNELS slice's middle.</summary>
    private static (string File, Point Channels) OpenToolsWheelOnASong(TriggerServiceLifecycleTests.DelayedProvider provider,
        RadialController radial, string folder)
    {
        var file = Path.Combine(folder, "song.mp3");
        var ffmpeg = Ffmpeg!;
        // Off the UI thread: the awaits inside would wait for the dispatcher this test is blocking.
        var made = Task.Run(() => ProcessRunner.RunAsync(ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y", "-f", "lavfi",
            "-i", "sine=frequency=440:duration=1", "-ac", "2", file], TimeSpan.FromMinutes(1), CancellationToken.None)).GetAwaiter().GetResult();
        Assert.True(made.ExitCode == 0, made.StandardError);
        provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
        provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
        Tap(provider, KeyCode.VcLeftControl);
        Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
        Drag(radial.Window!, file, RawDragEventType.DragEnter, CenterOf(radial.Window!));
        return (file, SliceCenter(radial.Window!.DonutControl, 5, 7));
    }

    /// <summary>The output exists and FFmpeg has let go of it.</summary>
    private static bool Finished(string file)
    {
        try
        {
            using var _ = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    [AvaloniaFact]
    public void A_drop_on_channels_asks_mono_or_stereo_and_the_pick_converts() => Run((provider, radial, vm, folder) =>
    {
        Assert.SkipUnless(Ffmpeg is not null, "FFmpeg is not installed");
        var (file, channels) = OpenToolsWheelOnASong(provider, radial, folder);
        Assert.Equal("Channels", vm.Items[5].Label);

        Drop(radial.Window!, file, channels);

        Assert.False(radial.Window!.IsVisible); // the wheel went, the popover is where the drop was
        var popover = radial.Popover;
        Assert.NotNull(popover);
        Assert.Equal("Channels", popover.Title);
        Assert.Equal(["Mono", "Stereo"], popover.Choices.Select(c => c.Label));
        Assert.Equal(0, popover.Selected);

        popover.Move(1);
        popover.Apply();

        Assert.Null(radial.Popover);
        Assert.True(Until(() => radial.Toast is not null));
        Assert.Equal("Channels Stereo", ((Filee.App.ViewModels.DropToastViewModel)radial.Toast!.DataContext!).Title);
        Assert.True(Until(() => Finished(Path.Combine(folder, "song Stereo.mp3")), 30_000));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void Escape_in_the_popover_converts_nothing() => Run((provider, radial, vm, folder) =>
    {
        Assert.SkipUnless(Ffmpeg is not null, "FFmpeg is not installed");
        var (file, channels) = OpenToolsWheelOnASong(provider, radial, folder);

        Drop(radial.Window!, file, channels);
        Assert.NotNull(radial.Popover);
        radial.Popover.Cancel();

        Assert.Null(radial.Popover);
        Until(() => false, 300);
        Assert.Null(radial.Toast);
        Assert.Equal(["song.mp3"], Directory.GetFiles(folder).Select(Path.GetFileName));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void Enter_on_channels_mid_drag_opens_the_popover_and_the_hook_keys_drive_it() => Run((provider, radial, vm, folder) =>
    {
        Assert.SkipUnless(Ffmpeg is not null, "FFmpeg is not installed");
        var (file, channels) = OpenToolsWheelOnASong(provider, radial, folder);

        // The drag rests on CHANNELS, Enter applies it: the popover opens, the wheel's window stays for the drop.
        Drag(radial.Window!, file, RawDragEventType.DragOver,
            radial.Window!.DonutControl.TranslatePoint(channels, radial.Window!)!.Value);
        Tap(provider, KeyCode.VcEnter);
        Assert.NotNull(radial.Popover);
        Assert.True(radial.Window!.IsVisible);

        Tap(provider, KeyCode.VcRight);
        Assert.Equal(1, radial.Popover!.Selected);
        Tap(provider, KeyCode.VcEscape);
        Assert.Null(radial.Popover);

        Drop(radial.Window!, file, channels); // the drop that ends the drag converts nothing
        Until(() => false, 300);
        Assert.Equal(["song.mp3"], Directory.GetFiles(folder).Select(Path.GetFileName));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });
}
