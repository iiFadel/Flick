// fork: the Trim editor (Phase 5) for videos and songs: a timeline of small frames (video) or the waveform (audio) with
// a start and an end handle, the frame under the handle being moved, the times, and "Remove silence" that moves the
// handles past the quiet at both ends. Apply cuts through the FFmpeg engine (MediaOptions.Edit) into "name Trimmed.ext".

using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Media;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public enum TrimHandle
{
    Start,
    End,
}

public sealed partial class TrimEditorViewModel(string title, string source, ILogger? log) : EditorViewModel(title, source, log)
{
    private const int StripFrames = 12;
    private const int StripHeight = 72;
    private const int PreviewHeight = 480;
    private const int WaveformPeaks = 600;

    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "Flick", "editor-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly CancellationTokenSource _closing = new();
    private CancellationTokenSource? _frameRequest;
    private int _frameNumber;

    [ObservableProperty] private double _duration;
    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private IReadOnlyList<Bitmap> _frames = [];
    [ObservableProperty] private float[] _peaks = [];
    [ObservableProperty] private Bitmap? _frame;
    [ObservableProperty] private TrimHandle _selected = TrimHandle.Start;
    [ObservableProperty] private bool _isFindingSilence;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartText), nameof(EndText), nameof(LengthText))]
    private TrimRange _range;

    private double _frameRate;

    public string StartText => TrimMath.Format(Range.Start);
    public string EndText => TrimMath.Format(Range.End);
    public string LengthText => TrimMath.Format(Range.Length);

    /// <summary>One arrow-key step: a frame of the video, a tenth of a second of a song.</summary>
    public double Step => TrimMath.Step(_frameRate);

    protected override bool HasChanges => Duration > 0 && !TrimMath.IsWhole(Range, Duration);

    protected override async Task LoadPreviewAsync()
    {
        Directory.CreateDirectory(_scratch);
        var summary = await MediaPreview.ProbeAsync(Source, _closing.Token);
        (Duration, IsVideo, _frameRate) = (summary.Duration, summary.HasVideo, summary.HasVideo ? summary.FrameRate : 0);
        Range = new TrimRange(0, Duration);
        if (IsVideo)
        {
            var folder = Directory.CreateDirectory(Path.Combine(_scratch, "strip")).FullName;
            var files = await MediaPreview.FramesAsync(Source, Duration, StripFrames, StripHeight, folder, _closing.Token);
            Frames = [.. files.Select(file => new Bitmap(file))];
            await ShowFrameAsync(0);
        }
        else
            Peaks = await MediaPreview.PeaksAsync(Source, WaveformPeaks, Path.Combine(_scratch, "sound.pcm"), _closing.Token);
        ChangesChanged();
    }

    /// <summary>The timeline dragged <paramref name="handle"/> to <paramref name="seconds"/>.</summary>
    public void Move(TrimHandle handle, double seconds)
    {
        Selected = handle;
        Range = handle == TrimHandle.Start ? TrimMath.MoveStart(Range, seconds, Duration) : TrimMath.MoveEnd(Range, seconds, Duration);
        ChangesChanged();
        _ = ShowFrameAsync(handle == TrimHandle.Start ? Range.Start : Range.End);
    }

    /// <summary>The selected handle a step (<paramref name="steps"/> of them) earlier or later.</summary>
    public void Nudge(int steps) =>
        Move(Selected, (Selected == TrimHandle.Start ? Range.Start : Range.End) + steps * Step);

    /// <summary>← → move the selected handle a step, with Shift a second.</summary>
    public override bool Arrow(int direction, bool large)
    {
        Nudge(direction * (large ? Math.Max(1, (int)Math.Round(1 / Step)) : 1));
        return true;
    }

    /// <summary>Tab picks the other handle.</summary>
    public override bool Tab()
    {
        Selected = Selected == TrimHandle.Start ? TrimHandle.End : TrimHandle.Start;
        return true;
    }

    [RelayCommand]
    private async Task RemoveSilenceAsync()
    {
        if (IsFindingSilence || Duration <= 0)
            return;
        IsFindingSilence = true;
        try
        {
            var silences = await MediaPreview.SilencesAsync(Source, Duration, _closing.Token);
            Range = TrimMath.WithoutSilentEnds(silences, Duration);
            ChangesChanged();
            _ = ShowFrameAsync(Range.Start);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.LogWarning(ex, "Could not find the silence in {File}", Source);
        }
        finally
        {
            IsFindingSilence = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Range = new TrimRange(0, Duration);
        Selected = TrimHandle.Start;
        ChangesChanged();
        _ = ShowFrameAsync(0);
    }

    public override void ApplyTo(Preset preset) => preset.Media.Edit = new MediaEdit { Trim = TrimMath.Clamp(Range, Duration) };

    /// <summary>Shows the frame at <paramref name="seconds"/> (video only); a newer request replaces one still running.</summary>
    private async Task ShowFrameAsync(double seconds)
    {
        if (!IsVideo)
            return;
        _frameRequest?.Cancel();
        var request = _frameRequest = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        try
        {
            await Task.Delay(60, request.Token); // while a handle is dragged, only where it rests
            var file = Path.Combine(_scratch, $"frame {++_frameNumber}.png");
            await MediaPreview.FrameAtAsync(Source, Math.Min(seconds, Math.Max(0, Duration - Step)), PreviewHeight, file, request.Token);
            if (request.IsCancellationRequested)
                return;
            var old = Frame;
            Frame = new Bitmap(file);
            old?.Dispose();
            TryDelete(file);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log?.LogDebug(ex, "No frame at {Seconds}s of {File}", seconds, Source);
        }
    }

    public override void Dispose()
    {
        _closing.Cancel();
        Frame?.Dispose();
        foreach (var frame in Frames)
            frame.Dispose();
        (Frame, Frames) = (null, []);
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

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
