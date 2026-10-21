// fork: after a drop on a drag wheel the wheel goes and a toast grows at the drop point (docs/TANGERINE-SPEC.md, "After
// the drop"), from the global hook to the converted file. Animations are off, so every step happens at once.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Conversion;
using Filee.Core.Platform;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using SharpHook.Data;

namespace Filee.App.Tests;

[Collection("Motion")]
public class DropToastTests
{
    private const string Captions = "1\n00:00:01,000 --> 00:00:02,500\nHello there\n\n2\n00:00:03,000 --> 00:00:04,000\nBye\n";

    [AvaloniaFact]
    public void A_drop_on_the_wheel_shows_the_toast_where_it_landed_until_the_file_is_saved() => Run((provider, radial, vm, folder) =>
    {
        var file = Path.Combine(folder, "captions.srt");
        File.WriteAllText(file, Captions);
        OpenConvertWheel(provider, radial, file);
        var window = radial.Window!;
        Assert.Equal(["VTT", "TXT"], vm.Items.Select(i => i.Label));

        // Drop on VTT (the top half).
        Drop(window, file, SliceCenter(window.DonutControl, 0, 2));

        Assert.False(window.IsVisible); // the wheel went
        var toast = radial.Toast;
        Assert.NotNull(toast);
        var model = Assert.IsType<DropToastViewModel>(toast!.DataContext);
        Assert.Equal("Converting to VTT", model.Title);
        // It grew where the drop landed: the toast's middle is the drop point.
        var middle = toast.PointToScreen(new Point(toast.Bounds.Width / 2, toast.Bounds.Height / 2));
        var dropped = window.DonutControl.PointToScreen(SliceCenter(window.DonutControl, 0, 2));
        Assert.True(Math.Abs(middle.X - dropped.X) <= 2 && Math.Abs(middle.Y - dropped.Y) <= 2,
            $"toast middle {middle}, drop {dropped}, toast at {toast.Position} size {toast.Bounds.Size}, screen {toast.Screens.ScreenFromPoint(dropped)?.WorkingArea}");

        Assert.True(Until(() => !toast.IsVisible), "the toast closes once the file is saved");
        Assert.True(model.Finished);
        Assert.Equal("1 of 1 saved", model.Status);
        Assert.True(File.Exists(Path.Combine(folder, "captions.vtt")));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void Enter_converts_and_the_drop_that_ends_the_drag_converts_nothing_more() => Run((provider, radial, vm, folder) =>
    {
        var file = Path.Combine(folder, "captions.srt");
        File.WriteAllText(file, Captions);
        OpenConvertWheel(provider, radial, file);
        var window = radial.Window!;

        Tap(provider, KeyCode.VcRight); // VTT
        Tap(provider, KeyCode.VcEnter);
        Assert.True(Until(() => radial.Toast is not null)); // a file this small may be done (and its toast gone) at once
        // The wheel's window stays, invisible, to take the drop that ends the drag.
        Assert.True(window.IsVisible);
        Assert.Equal(0, vm.DonutOpacity);
        Assert.Empty(vm.Items);

        Drop(window, file, SliceCenter(window.DonutControl, 1, 2));
        Assert.False(window.IsVisible);
        Assert.True(Until(() => !radial.Toast!.IsVisible));
        Assert.Equal(["captions.srt", "captions.vtt"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void The_toast_shows_the_title_the_file_and_the_bar()
    {
        TestServices.EnsureInitialized("en");
        var job = new JobViewModel(new ConversionJob(["C:/sample.wav"], new Preset { TargetFormat = "mp3" }, "MP3"),
            AppHost.Get<Filee.Core.Localization.ILocalizer>(), AppHost.Get<IPlatformServices>(), _ => { }) { Progress = 42 };
        var model = new DropToastViewModel(job, "Converting to MP3", AppHost.Get<Filee.Core.Localization.ILocalizer>());
        var toast = new DropToastWindow { DataContext = model };
        try
        {
            Motion.Configure(true, () => false);
            toast.ShowAt(new PixelPoint(600, 400));
            for (var i = 0; i < 5; i++)
                Dispatcher.UIThread.RunJobs();
            Assert.Equal("sample.wav · 42%", model.Status);
            Assert.Equal(0.42, model.Fraction, 6);
            Assert.Equal(1, toast.FindControl<Border>("Card")!.Opacity);
            RenderTests.Save(toast, "flick-drop-toast.png");
        }
        finally
        {
            Motion.Configure(false, () => false);
            toast.Close();
        }
    }

    /// <summary>A hook, a controller listening to it and a folder for the files, with animations off.</summary>
    private static void Run(Action<TriggerServiceLifecycleTests.DelayedProvider, RadialController, RadialViewModel, string> test)
    {
        TestServices.EnsureInitialized("en");
        Motion.Configure(true, () => false);
        var provider = new TriggerServiceLifecycleTests.DelayedProvider();
        provider.AllowExit.Set();
        using var triggers = TriggerServiceLifecycleTests.Create(provider, () => { });
        triggers.Apply(new AppSettings { DragWheel = { ConvertKey = "Shift", ToolsKey = "Ctrl" } });
        triggers.Start();
        Assert.True(SpinWait.SpinUntil(() => provider.Runs == 1, 2000));
        var vm = AppHost.Get<RadialViewModel>();
        var radial = new RadialController(vm, AppHost.Get<ConversionService>(), AppHost.Get<WindowService>(),
            AppHost.Get<UserDataStore>(), AppHost.Get<IPlatformServices>(), triggers, NullLogger<RadialController>.Instance);
        radial.ListenToWheels();
        var folder = Directory.CreateTempSubdirectory("flick-toast-");
        try
        {
            test(provider, radial, vm, folder.FullName);
        }
        finally
        {
            radial.Window?.Close();
            radial.Toast?.Close();
            Motion.Configure(false, () => false);
            folder.Delete(recursive: true);
        }
    }

    private static void OpenConvertWheel(TriggerServiceLifecycleTests.DelayedProvider provider, RadialController radial, string file)
    {
        provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
        provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
        Tap(provider, KeyCode.VcLeftShift);
        Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
        Drag(radial.Window!, file, RawDragEventType.DragEnter, CenterOf(radial.Window!));
    }

    private static void Tap(TriggerServiceLifecycleTests.DelayedProvider provider, KeyCode key)
    {
        provider.PostKey(EventType.KeyPressed, key, EventMask.Button1);
        provider.PostKey(EventType.KeyReleased, key, EventMask.Button1);
        TriggerServiceLifecycleTests.Settle(() => 0);
    }

    private static Point CenterOf(Window window) => new(window.Bounds.Width / 2, window.Bounds.Height / 2);

    /// <summary>The middle of slice <paramref name="index"/> of <paramref name="count"/>, in the donut's coordinates.</summary>
    private static Point SliceCenter(DonutMenu donut, int index, int count) =>
        DonutGeometry.PointAt(new Point(donut.Bounds.Width / 2, donut.Bounds.Height / 2), donut.Layout.LabelRadius,
            DonutGeometry.MidAngle(index, count));

    private static void Drop(RadialWindow window, string file, Point onDonut)
    {
        var at = window.DonutControl.TranslatePoint(onDonut, window)!.Value;
        Drag(window, file, RawDragEventType.DragOver, at);
        Drag(window, file, RawDragEventType.Drop, at);
    }

    private static void Drag(RadialWindow window, string file, RawDragEventType type, Point at)
    {
        var data = new DataTransfer();
        var item = window.StorageProvider.TryGetFileFromPathAsync(file).GetAwaiter().GetResult();
        Assert.NotNull(item);
        data.Add(DataTransferItem.CreateFile(item));
        window.DragDrop(at, type, data, DragDropEffects.Copy, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs the UI thread until <paramref name="condition"/> holds (a conversion runs on other threads).</summary>
    private static bool Until(Func<bool> condition, int timeoutMs = 10_000)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
                return true;
            Thread.Sleep(10);
        }
        return false;
    }
}
