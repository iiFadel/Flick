// fork: the Edit editor (Phase 5): exposure, contrast, saturation, warmth and sharpness sliders over the picture, which
// shows each change on a small copy run through the same engine code as the file (ImageEdits.Adjust); hold Original
// to see the picture as it was. Apply writes "name Edited.ext" in the picture's own format.

using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Magick;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public sealed partial class AdjustEditorViewModel(string title, string source, ILogger? log) : EditorViewModel(title, source, log)
{
    private const uint PreviewSide = 1400;
    private MagickImage? _base;
    private Bitmap? _original;
    private int _version;

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private int _exposure;
    [ObservableProperty] private int _contrast;
    [ObservableProperty] private int _saturation;
    [ObservableProperty] private int _warmth;
    [ObservableProperty] private int _sharpness;

    /// <summary>True while Original is held: the picture as it was shows.</summary>
    [ObservableProperty] private bool _showOriginal;

    /// <summary>The picture on screen: the adjusted preview, or the original while Original is held.</summary>
    public Bitmap? Shown => ShowOriginal ? _original : Preview ?? _original;

    public Adjustments Adjustments => new Adjustments
    {
        Exposure = Exposure,
        Contrast = Contrast,
        Saturation = Saturation,
        Warmth = Warmth,
        Sharpness = Sharpness,
    }.Clamped();

    protected override bool HasChanges => !Adjustments.IsNone;

    /// <summary>Finishes once the newest preview is on screen (tests).</summary>
    internal Task Rendered { get; private set; } = Task.CompletedTask;

    protected override async Task LoadPreviewAsync()
    {
        var (bitmap, _, _) = await CropService.LoadPreviewAsync(Source);
        _original = bitmap;
        _base = await Task.Run(() =>
        {
            using var png = new MemoryStream();
            bitmap.Save(png);
            png.Position = 0;
            var image = new MagickImage(png);
            image.Resize(new MagickGeometry(PreviewSide, PreviewSide) { Greater = true });
            return image;
        });
        OnPropertyChanged(nameof(Shown));
        if (!Adjustments.IsNone) // a slider moved while the copy was being made
            Rendered = RenderAsync();
    }

    partial void OnExposureChanged(int value) => Changed();
    partial void OnContrastChanged(int value) => Changed();
    partial void OnSaturationChanged(int value) => Changed();
    partial void OnWarmthChanged(int value) => Changed();
    partial void OnSharpnessChanged(int value) => Changed();
    partial void OnShowOriginalChanged(bool value) => OnPropertyChanged(nameof(Shown));
    partial void OnPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(Shown));

    [RelayCommand]
    private void Reset() => (Exposure, Contrast, Saturation, Warmth, Sharpness) = (0, 0, 0, 0, 0);

    public override void ApplyTo(Preset preset) => preset.Image.Edit = new ImageEdit { Adjust = Adjustments };

    private void Changed()
    {
        ChangesChanged();
        Rendered = RenderAsync();
    }

    /// <summary>Runs the sliders on the small copy; a newer change makes an older run drop its result.</summary>
    private async Task RenderAsync()
    {
        if (_base is not { } source)
            return;
        var version = ++_version;
        var adjust = Adjustments;
        await Task.Delay(40); // while a slider is dragged, only where it rests
        if (version != _version)
            return;
        try
        {
            var png = await Task.Run(() =>
            {
                using var copy = source.Clone();
                if (!adjust.IsNone)
                    ImageEdits.Adjust(copy, adjust);
                using var stream = new MemoryStream();
                copy.Write(stream, MagickFormat.Png);
                return stream.ToArray();
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _version || _base is null)
                    return;
                using var stream = new MemoryStream(png);
                var old = Preview;
                Preview = new Bitmap(stream);
                old?.Dispose();
            });
        }
        catch (Exception ex) when (ex is MagickException or ObjectDisposedException)
        {
            Log?.LogDebug(ex, "No preview of the edit of {File}", Source);
        }
    }

    public override void Dispose()
    {
        _version++;
        _base?.Dispose();
        _base = null;
        Preview?.Dispose();
        _original?.Dispose();
        (Preview, _original) = (null, null);
        base.Dispose();
    }
}
