// fork: the Crop editor (Phase 5): the picture with a crop box on it (Controls/CropCanvas), an aspect ratio to keep, or
// an exact size in pixels. Apply crops through the image engine (ImageOptions.Edit) into "name Cropped.ext".

using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public partial class CropEditorViewModel(string title, string source, ILogger? log) : EditorViewModel(title, source, log)
{
    /// <summary>The aspect choices, in the order the window shows them.</summary>
    public static readonly string[] Aspects = ["free", "original", "1:1", "4:3", "16:9", "9:16"];

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private int _pixelWidth;
    [ObservableProperty] private int _pixelHeight;
    [ObservableProperty] private string _aspect = "free";
    [ObservableProperty] private string _widthText = "";
    [ObservableProperty] private string _heightText = "";

    private NormalizedRect _crop = NormalizedRect.Full;

    /// <summary>The part of the picture to keep.</summary>
    public NormalizedRect Crop => _crop;

    /// <summary>The canvas should keep this width / height (0: free).</summary>
    public event Action<double>? AspectRequested;

    /// <summary>The canvas should show this crop (an exact size was typed, or Reset).</summary>
    public event Action<NormalizedRect>? CropRequested;

    /// <summary>True for video: the sizes shown are rounded down to even, as the crop will be.</summary>
    protected virtual bool EvenSizes => false;

    protected override bool HasChanges => CropMath.ToPixels(_crop, PixelWidth, PixelHeight) != new PixelRect(0, 0, PixelWidth, PixelHeight);

    protected override async Task LoadPreviewAsync()
    {
        var (bitmap, width, height) = await CropService.LoadPreviewAsync(Source);
        ShowPreview(bitmap, width, height);
    }

    /// <summary>The picture to crop and its size in the file's own pixels.</summary>
    protected void ShowPreview(Bitmap bitmap, int width, int height)
    {
        (Preview, PixelWidth, PixelHeight) = (bitmap, width, height);
        Reset();
    }

    /// <summary>The canvas moved or resized the box.</summary>
    public void CropChanged(NormalizedRect crop)
    {
        _crop = CropMath.Clamp(crop);
        var pixels = CropMath.ToPixels(_crop, PixelWidth, PixelHeight, EvenSizes);
        WidthText = pixels.Width.ToString(CultureInfo.InvariantCulture);
        HeightText = pixels.Height.ToString(CultureInfo.InvariantCulture);
        ChangesChanged();
    }

    [RelayCommand]
    private void SetAspect(string aspect)
    {
        Aspect = aspect;
        AspectRequested?.Invoke(CropMath.AspectRatio(aspect, PixelWidth, PixelHeight));
    }

    /// <summary>The width and height typed: the box takes that size (free aspect), as close to its centre as fits.</summary>
    [RelayCommand]
    private void SetExactSize()
    {
        if (!int.TryParse(WidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) || width <= 0
            || !int.TryParse(HeightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) || height <= 0)
        {
            CropChanged(_crop); // back to the box's own numbers
            return;
        }
        SetAspect("free");
        var crop = CropMath.Exact(width, height, PixelWidth, PixelHeight, _crop);
        CropRequested?.Invoke(crop);
        CropChanged(crop);
    }

    [RelayCommand]
    private void Reset()
    {
        SetAspect("free");
        CropRequested?.Invoke(NormalizedRect.Full);
        CropChanged(NormalizedRect.Full);
    }

    public override void ApplyTo(Preset preset) => preset.Image.Edit = new ImageEdit { Crop = _crop };

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose();
    }
}
