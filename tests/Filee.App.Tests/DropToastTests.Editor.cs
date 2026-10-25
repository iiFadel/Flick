// fork: a tool with an editor window (Crop) opens it on the file dropped; Apply starts the job and the drop toast follows,
// Cancel runs nothing.

using Avalonia.Headless.XUnit;
using Avalonia.Input.Raw;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;
using ImageMagick;
using SharpHook.Data;

namespace Filee.App.Tests;

public partial class DropToastTests
{
    /// <summary>A 320 × 200 PNG, its Tools wheel open; returns the CROP slice's middle.</summary>
    private static (string File, Avalonia.Point Crop) OpenToolsWheelOnAPicture(TriggerServiceLifecycleTests.DelayedProvider provider,
        RadialController radial, string folder)
    {
        var file = Path.Combine(folder, "photo.png");
        using (var image = new MagickImage(MagickColors.Coral, 320, 200))
            image.Write(file, MagickFormat.Png);
        provider.PostMouse(EventType.MousePressed, 400, 400, EventMask.Button1);
        provider.PostMouse(EventType.MouseDragged, 460, 400, EventMask.Button1);
        Tap(provider, KeyCode.VcLeftControl);
        Assert.True(TriggerServiceLifecycleTests.Settle(() => radial.IsVisible));
        Drag(radial.Window!, file, RawDragEventType.DragEnter, CenterOf(radial.Window!));
        return (file, SliceCenter(radial.Window!.DonutControl, 5, 7));
    }

    [AvaloniaFact]
    public void A_drop_on_crop_opens_the_editor_and_apply_crops() => Run((provider, radial, vm, folder) =>
    {
        var (file, crop) = OpenToolsWheelOnAPicture(provider, radial, folder);
        Assert.Equal("Crop", vm.Items[5].Label);

        Drop(radial.Window!, file, crop);

        Assert.False(radial.Window!.IsVisible);
        var editor = Assert.IsType<CropEditorViewModel>(radial.Editor);
        Assert.Equal("Crop", editor.Title);
        Assert.True(Until(() => editor.PixelWidth == 320));
        Assert.False(editor.ApplyCommand.CanExecute(null)); // the whole picture: nothing to crop yet

        editor.CropChanged(new NormalizedRect(0, 0, 0.5, 0.5));
        Assert.Equal(("160", "100"), (editor.WidthText, editor.HeightText));
        editor.ApplyCommand.Execute(null);

        Assert.Null(radial.Editor);
        Assert.True(Until(() => radial.Toast is not null));
        var output = Path.Combine(folder, "photo Cropped.png");
        Assert.True(Until(() => Finished(output), 30_000));
        using var result = new MagickImage(output);
        Assert.Equal((160u, 100u), (result.Width, result.Height));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void Cancel_in_the_editor_converts_nothing() => Run((provider, radial, vm, folder) =>
    {
        var (file, crop) = OpenToolsWheelOnAPicture(provider, radial, folder);

        Drop(radial.Window!, file, crop);
        var editor = Assert.IsType<CropEditorViewModel>(radial.Editor);
        Assert.True(Until(() => editor.PixelWidth == 320));
        editor.CropChanged(new NormalizedRect(0, 0, 0.5, 0.5));
        editor.Cancel();

        Assert.Null(radial.Editor);
        Until(() => false, 300);
        Assert.Null(radial.Toast);
        Assert.Equal(["photo.png"], Directory.GetFiles(folder).Select(Path.GetFileName));
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });

    [AvaloniaFact]
    public void An_exact_size_typed_sets_the_crop() => Run((provider, radial, vm, folder) =>
    {
        var (file, crop) = OpenToolsWheelOnAPicture(provider, radial, folder);
        Drop(radial.Window!, file, crop);
        var editor = Assert.IsType<CropEditorViewModel>(radial.Editor);
        Assert.True(Until(() => editor.PixelWidth == 320));

        (editor.WidthText, editor.HeightText) = ("100", "50");
        editor.SetExactSizeCommand.Execute(null);

        Assert.Equal(new PixelRect(110, 75, 100, 50), CropMath.ToPixels(editor.Crop, 320, 200));
        Assert.True(editor.ApplyCommand.CanExecute(null));
        editor.Cancel();
        provider.PostMouse(EventType.MouseReleased, 460, 400, EventMask.None);
    });
}
