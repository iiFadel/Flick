// fork: the Redact editor (Phase 5) for pictures and videos: draw boxes over what must not be seen, Solid (black) or
// Blur; click a box to pick it, Delete removes it, Ctrl+Z takes back the last. A video shows its middle frame and the
// boxes cover every frame. Apply hides them through the image or FFmpeg engine into "name Redacted.ext".

using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Media;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public sealed partial class RedactEditorViewModel(string title, string source, bool video, ILogger? log) : EditorViewModel(title, source, log)
{
    private const int VideoPreviewHeight = 1080;
    private readonly RedactRegions _regions = new();
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "Flick", "editor-" + Guid.NewGuid().ToString("N")[..8]);

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private IReadOnlyList<NormalizedRect> _boxes = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private int _selected = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolid), nameof(IsBlur))]
    private RedactStyle _style = RedactStyle.Solid;

    public bool IsVideo => video;
    public bool IsSolid => Style == RedactStyle.Solid;
    public bool IsBlur => Style == RedactStyle.Blur;

    protected override bool HasChanges => _regions.Boxes.Count > 0;

    protected override async Task LoadPreviewAsync()
    {
        if (!video)
        {
            (Preview, _, _) = await CropService.LoadPreviewAsync(Source);
            return;
        }
        Directory.CreateDirectory(_scratch);
        var summary = await MediaPreview.ProbeAsync(Source, CancellationToken.None);
        if (!summary.HasVideo)
            throw new NotSupportedException("The file has no video.");
        var file = Path.Combine(_scratch, "frame.png");
        await MediaPreview.FrameAtAsync(Source, summary.Duration / 2, VideoPreviewHeight, file, CancellationToken.None);
        Preview = new Bitmap(file);
    }

    /// <summary>A box dragged from one corner to the other (fractions of the picture).</summary>
    public void Draw(double x1, double y1, double x2, double y2)
    {
        var index = _regions.Add(x1, y1, x2, y2);
        if (index >= 0)
            Selected = index;
        Refresh();
    }

    /// <summary>A click picks the box under it, or none.</summary>
    public void Pick(double x, double y) => Selected = _regions.At(x, y);

    [RelayCommand]
    private void SetStyle(RedactStyle style) => Style = style;

    private bool HasSelection() => Selected >= 0;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        _regions.Remove(Selected);
        Selected = -1;
        Refresh();
    }

    [RelayCommand]
    private void Clear()
    {
        _regions.Clear();
        Selected = -1;
        Refresh();
    }

    public override bool Undo()
    {
        if (!_regions.Undo())
            return false;
        Selected = -1;
        Refresh();
        return true;
    }

    public override bool Delete()
    {
        if (!HasSelection())
            return false;
        Remove();
        return true;
    }

    public override void ApplyTo(Preset preset)
    {
        if (video)
            preset.Media.Edit = new MediaEdit { Redactions = [.. _regions.Boxes], RedactStyle = Style };
        else
            preset.Image.Edit = new ImageEdit { Redactions = [.. _regions.Boxes], RedactStyle = Style };
    }

    private void Refresh()
    {
        Boxes = [.. _regions.Boxes];
        ChangesChanged();
    }

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        try
        {
            if (Directory.Exists(_scratch))
                Directory.Delete(_scratch, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.LogDebug(ex, "Could not delete {Folder}", _scratch);
        }
        base.Dispose();
    }
}
