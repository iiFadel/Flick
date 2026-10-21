// fork: the drag wheels in the donut. Each wheel shows the spec's slices with none selected when it opens, keys from
// the global hook hold the selection until the drag moves on, a conversion's ring takes no drop, the whole path from the
// hook (drag, tap, switch, arrows, Escape) drives the controller, and Ctrl does nothing for files without tools.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.ViewModels.Pages;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Profiles;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using SharpHook.Data;

namespace Filee.App.Tests;

[Collection("Motion")]
public class DragWheelTests
{
    private static readonly string[] Photos = [@"C:\photos\a.jpg", @"C:\photos\b.jpg"];

    private static UserDataStore Store => AppHost.Get<UserDataStore>();

    // fork: the wheels show the spec's lists (WheelContentsTests covers every category).
    private static readonly string[] PhotoTargets = ["PNG", "WEBP", "HEIC", "TIFF", "AVIF", "BMP", "PDF"];
    private static readonly string[] PhotoTools = ["Compress", "Metadata", "Edit", "Annotate", "Add BG", "Crop", "Redact"];
    private static readonly string[] PhotosTools = ["Compress", "Metadata", "Crop", "Create PDF", "Collage"];
    private static readonly string[] LetterTargets = ["PAGES", "PDF", "TXT", "MD", "HTML", "EPUB", "PNG", "JPG"];

    private static void WithoutAnimations(Action test)
    {
        TestServices.EnsureInitialized("en");
        try
        {
            Motion.Configure(true, () => false);
            test();
        }
        finally
        {
            Motion.Configure(false, () => false);
        }
    }

    /// <summary>A drag-mode donut with <see cref="Photos"/> on it, as the controller has it before a wheel is chosen.</summary>
    private static (RadialViewModel Vm, RadialWindow Window, DonutMenu Donut) OpenDragMode()
    {
        var vm = AppHost.Get<RadialViewModel>();
        vm.ResetForDrag();
        var window = new RadialWindow { DataContext = vm };
        window.DonutControl.ActiveIndexChanged += (_, index) =>
            vm.ShowHint(index, window.DonutControl.ActiveSubIndex, window.DonutControl.IsCenterHot);
        window.Show();
        vm.Load(Photos, clickMode: false);
        Dispatcher.UIThread.RunJobs();
        return (vm, window, window.DonutControl);
    }

    private static Point Center(DonutMenu donut) => new(donut.Bounds.Width / 2, donut.Bounds.Height / 2);

    private static Point SliceAt(DonutMenu donut, RadialViewModel vm, int index) =>
        DonutGeometry.PointAt(Center(donut), vm.OuterRadius * (1 + vm.HoleRatio) / 2, DonutGeometry.MidAngle(index, vm.Items.Count));

    [AvaloniaFact]
    public void Each_wheel_shows_its_slices_and_none_flips() => WithoutAnimations(() =>
    {
        var (vm, window, _) = OpenDragMode();

        vm.ShowWheel(WheelKind.Convert);
        Assert.Equal(PhotoTargets, vm.Items.Select(i => i.Label));
        Assert.False(vm.IsBackFace);
        Assert.False(vm.CanFlip);
        Assert.Null(vm.FlipHint);
        Assert.Null(vm.FlipKey);

        vm.ShowWheel(WheelKind.Tools);
        Assert.Equal(PhotosTools, vm.Items.Select(i => i.Label));
        Assert.False(vm.CanFlip);
        Assert.Null(vm.FlipHint);

        // The check of the back face posted by Load must not bring the front back.
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PhotosTools, vm.Items.Select(i => i.Label));
        window.Close();
    });

    [AvaloniaFact]
    public void Keys_hold_the_selection_until_the_drag_moves_on() => WithoutAnimations(() =>
    {
        var (vm, window, donut) = OpenDragMode();
        vm.ShowWheel(WheelKind.Convert);
        var center = Center(donut);
        donut.SetExternalHit(donut.HitTest(center), center);

        Assert.True(donut.HandleDragKey(Key.Right)); // the first arrow picks the top slice
        Assert.Equal(0, donut.ActiveIndex);
        donut.SetExternalHit(donut.HitTest(center), center); // the drag rests in the hole
        Assert.Equal(0, donut.ActiveIndex);

        Assert.True(donut.HandleDragKey(Key.Right));
        var nudged = center + new Point(2, 1);
        donut.SetExternalHit(donut.HitTest(nudged), nudged);
        Assert.Equal(1, donut.ActiveIndex);
        Assert.True(donut.HandleDragKey(Key.Left));
        Assert.True(donut.HandleDragKey(Key.Left));
        Assert.Equal(vm.Items.Count - 1, donut.ActiveIndex);

        // Moving on purpose: the drag has the selection again; back in the hole nothing is selected (a drop there cancels).
        var slice = SliceAt(donut, vm, 3);
        donut.SetExternalHit(donut.HitTest(slice), slice);
        Assert.Equal(3, donut.ActiveIndex);
        donut.SetExternalHit(donut.HitTest(center), center);
        Assert.Equal(-1, donut.ActiveIndex);
        Assert.True(donut.IsCenterHot);
        window.Close();
    });

    [AvaloniaFact]
    public void A_conversion_ring_takes_no_drop() => WithoutAnimations(() =>
    {
        var (vm, window, donut) = OpenDragMode();
        vm.ShowWheel(WheelKind.Convert);
        var hit = donut.HitTest(SliceAt(donut, vm, 0));
        Assert.True(donut.Converts(hit));

        donut.BeginProgress();

        Assert.False(donut.Converts(hit));
        Assert.False(donut.HandleDragKey(Key.Enter));
        window.Close();
    });

    [AvaloniaFact]
    public void Settings_record_a_wheel_key_and_refuse_one_in_use() => WithoutAnimations(() =>
    {
        var loc = AppHost.Get<ILocalizer>();
        var before = Store.Settings.DragWheel.Clone();
        try
        {
            var page = new TriggersPageViewModel(Store, loc);
            var (convert, tools) = (page.Wheel.Keys[0], page.Wheel.Keys[1]);
            Assert.Equal(WheelKind.Convert, convert.Kind);
            Assert.Equal("Shift", convert.KeyText);

            tools.IsRecording = true;
            Assert.Equal(loc["triggers.recording_key"], tools.KeyText);
            Assert.True(tools.Record(Key.RightShift)); // Convert's key
            Assert.Equal(loc.Format("triggers.wheel_key_taken", "Shift"), page.Wheel.Error);
            Assert.False(tools.IsRecording);

            tools.IsRecording = true;
            Assert.True(tools.Record(Key.Enter)); // applies the selected slice
            Assert.NotNull(page.Wheel.Error);

            tools.IsRecording = true;
            Assert.True(tools.Record(Key.F2));
            Assert.Null(page.Wheel.Error);
            Assert.Equal("F2", tools.KeyText);
            Assert.Equal("F2", Store.Settings.DragWheel.ToolsKey);

            tools.ResetCommand.Execute(null);
            Assert.Equal(DragWheelSettings.DefaultKey(WheelKind.Tools), Store.Settings.DragWheel.ToolsKey);
        }
        finally
        {
            Store.Settings.DragWheel = before;
        }
    });

    /// <summary>
    /// From the global hook (a stand-in for libuiohook) to the donut: drag, tap Shift, the files enter, Ctrl switches,
    /// an arrow moves, Escape closes. No conversion is started.
    /// </summary>
    [AvaloniaFact]
    public void A_drag_and_a_tap_open_the_wheel_and_the_hook_drives_it() => WithoutAnimations(() =>
    {
        var provider = new TriggerServiceLifecycleTests.DelayedProvider();
        provider.AllowExit.Set();
        using var triggers = TriggerServiceLifecycleTests.Create(provider, () => { });
        triggers.Apply(new AppSettings { DragWheel = { ConvertKey = "Shift", ToolsKey = "Ctrl" } });
        triggers.Start();
        Assert.True(SpinWait.SpinUntil(() => provider.Runs == 1, 2000));
        var vm = AppHost.Get<RadialViewModel>();
        var radial = new RadialController(vm, AppHost.Get<ConversionService>(), AppHost.Get<WindowService>(), Store,
            AppHost.Get<IPlatformServices>(), triggers, NullLogger<RadialController>.Instance);
        radial.ListenToWheels();
        var folder = Directory.CreateTempSubdirectory("flick-wheel-");
        try
        {
            var file = Path.Combine(folder.FullName, "photo.jpg");
            File.WriteAllBytes(file, [0xFF, 0xD8, 0xFF]);

            provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
            provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible)); // a plain drag
            provider.PostKey(EventType.KeyPressed, KeyCode.VcLeftShift, EventMask.Button1);
            provider.PostKey(EventType.KeyReleased, KeyCode.VcLeftShift, EventMask.Button1);
            Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));

            var window = radial.Window!;
            var data = new DataTransfer();
            var item = window.StorageProvider.TryGetFileFromPathAsync(file).GetAwaiter().GetResult();
            Assert.NotNull(item);
            data.Add(DataTransferItem.CreateFile(item));
            var at = new Point(window.Bounds.Width / 2, window.Bounds.Height / 2);
            window.DragDrop(at, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new[] { file }, vm.Files);
            Assert.Equal(PhotoTargets, vm.Items.Select(i => i.Label));
            Assert.Equal(-1, window.DonutControl.ActiveIndex); // fork: nothing is selected until the drag or a key picks a slice
            Assert.False(triggers.WatchFlipKey); // the wheels replace flipping

            provider.PostKey(EventType.KeyPressed, KeyCode.VcLeftControl, EventMask.Button1);
            Assert.Equal(PhotoTools, TriggerServiceLifecycleTests.Settle(() => vm.Items.Select(i => i.Label).ToList()));
            Assert.Equal(-1, window.DonutControl.ActiveIndex);
            provider.PostKey(EventType.KeyReleased, KeyCode.VcLeftControl, EventMask.Button1);

            // Enter with nothing selected does nothing; the first arrow selects the top slice, the next moves on.
            provider.PostKey(EventType.KeyPressed, KeyCode.VcEnter, EventMask.Button1);
            Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
            Assert.Equal(-1, window.DonutControl.ActiveIndex);
            provider.PostKey(EventType.KeyPressed, KeyCode.VcRight, EventMask.Button1);
            Assert.Equal(0, TriggerServiceLifecycleTests.Settle(() => window.DonutControl.ActiveIndex));
            provider.PostKey(EventType.KeyReleased, KeyCode.VcRight, EventMask.Button1);
            provider.PostKey(EventType.KeyPressed, KeyCode.VcRight, EventMask.Button1);
            Assert.Equal(1, TriggerServiceLifecycleTests.Settle(() => window.DonutControl.ActiveIndex));

            // fork: Alt opens no wheel (Flick has no Actions wheel): the Tools wheel stays.
            provider.PostKey(EventType.KeyPressed, KeyCode.VcLeftAlt, EventMask.Button1);
            provider.PostKey(EventType.KeyReleased, KeyCode.VcLeftAlt, EventMask.Button1);
            Assert.Equal(PhotoTools, TriggerServiceLifecycleTests.Settle(() => vm.Items.Select(i => i.Label).ToList()));

            provider.PostKey(EventType.KeyPressed, KeyCode.VcEscape, EventMask.Button1);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
            provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
            window.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    });

    /// <summary>
    /// fork: files without tools (a Word document). Ctrl does nothing over the Convert wheel; a drag opened with Ctrl
    /// shows nothing until Shift opens the Convert wheel.
    /// </summary>
    [AvaloniaFact]
    public void Ctrl_does_nothing_for_files_without_tools() => WithoutAnimations(() =>
    {
        var provider = new TriggerServiceLifecycleTests.DelayedProvider();
        provider.AllowExit.Set();
        using var triggers = TriggerServiceLifecycleTests.Create(provider, () => { });
        triggers.Apply(new AppSettings { DragWheel = { ConvertKey = "Shift", ToolsKey = "Ctrl" } });
        triggers.Start();
        Assert.True(SpinWait.SpinUntil(() => provider.Runs == 1, 2000));
        var vm = AppHost.Get<RadialViewModel>();
        var radial = new RadialController(vm, AppHost.Get<ConversionService>(), AppHost.Get<WindowService>(), Store,
            AppHost.Get<IPlatformServices>(), triggers, NullLogger<RadialController>.Instance);
        radial.ListenToWheels();
        var folder = Directory.CreateTempSubdirectory("flick-wheel-");
        try
        {
            var file = Path.Combine(folder.FullName, "letter.docx");
            File.WriteAllBytes(file, [0x50, 0x4B, 0x03, 0x04]);
            List<string> Labels() => vm.Items.Select(i => i.Label).ToList();
            void Tap(KeyCode key)
            {
                provider.PostKey(EventType.KeyPressed, key, EventMask.Button1);
                provider.PostKey(EventType.KeyReleased, key, EventMask.Button1);
            }

            // Shift first: the Convert wheel. Ctrl leaves it as it is.
            provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
            provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
            Tap(KeyCode.VcLeftShift);
            Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
            Enter(radial.Window!, file);
            Assert.Equal(LetterTargets, Labels());
            Tap(KeyCode.VcLeftControl);
            Assert.Equal(LetterTargets, TriggerServiceLifecycleTests.Settle(Labels));
            Assert.True(radial.IsVisible);
            Assert.Equal(1, vm.DonutOpacity);
            Tap(KeyCode.VcEscape);
            Assert.False(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
            provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);

            // Ctrl first: nothing shows; Shift opens the Convert wheel.
            provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
            provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
            Tap(KeyCode.VcLeftControl);
            Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
            Enter(radial.Window!, file);
            Assert.Empty(vm.Items);
            Assert.Equal(0, vm.DonutOpacity);
            Tap(KeyCode.VcLeftShift);
            Assert.Equal(LetterTargets, TriggerServiceLifecycleTests.Settle(Labels));
            Assert.Equal(1, vm.DonutOpacity);

            Tap(KeyCode.VcEscape);
            provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
            radial.Window!.Close();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    });

    /// <summary>The OS drag carrying <paramref name="file"/> reaches the wheel's window.</summary>
    private static void Enter(RadialWindow window, string file)
    {
        var data = new DataTransfer();
        var item = window.StorageProvider.TryGetFileFromPathAsync(file).GetAwaiter().GetResult();
        Assert.NotNull(item);
        data.Add(DataTransferItem.CreateFile(item));
        var at = new Point(window.Bounds.Width / 2, window.Bounds.Height / 2);
        window.DragDrop(at, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }
}
