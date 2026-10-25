// fork: the Crop editor on a video (Phase 5): the Crop image editor on the video's middle frame, its sizes in the
// video's own pixels, rounded down to even. Apply crops every frame through the FFmpeg engine (MediaOptions.Edit) into
// "name Cropped.ext", re-encoded in the source's format.

using System.IO;
using Avalonia.Media.Imaging;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Media;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public sealed class CropVideoEditorViewModel(string title, string source, ILogger? log) : CropEditorViewModel(title, source, log)
{
    private const int PreviewHeight = 1080;
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "Flick", "editor-" + Guid.NewGuid().ToString("N")[..8]);

    protected override bool EvenSizes => true;

    protected override async Task LoadPreviewAsync()
    {
        Directory.CreateDirectory(_scratch);
        var summary = await MediaPreview.ProbeAsync(Source, CancellationToken.None);
        if (!summary.HasVideo)
            throw new NotSupportedException("The file has no video.");
        var file = Path.Combine(_scratch, "frame.png");
        await MediaPreview.FrameAtAsync(Source, summary.Duration / 2, PreviewHeight, file, CancellationToken.None);
        ShowPreview(new Bitmap(file), summary.Width, summary.Height);
    }

    public override void ApplyTo(Preset preset) => preset.Media.Edit = new MediaEdit { Crop = Crop };

    public override void Dispose()
    {
        base.Dispose();
        try
        {
            if (Directory.Exists(_scratch))
                Directory.Delete(_scratch, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log?.LogDebug(ex, "Could not delete {Folder}", _scratch);
        }
    }
}
