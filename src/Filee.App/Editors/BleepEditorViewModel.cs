// fork: the Bleep editor (Phase 5): the song's waveform; drag over a word to mark it, click a marked range to pick it,
// Delete removes it, Ctrl+Z takes back the last change. Tone covers the ranges with a 1 kHz beep, Silence mutes them.
// Apply re-encodes through the FFmpeg engine (MediaOptions.Edit) into "name Bleeped.ext". Marking by hand: Tangerine's
// Bleep has no automatic detection either.

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Media;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public sealed partial class BleepEditorViewModel(string title, string source, ILogger? log) : EditorViewModel(title, source, log)
{
    private const int WaveformPeaks = 900;
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "Flick", "editor-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Stack<TrimRange[]> _undo = new();
    private BleepRanges _marks = new(0);

    [ObservableProperty] private double _duration;
    [ObservableProperty] private float[] _peaks = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyPropertyChangedFor(nameof(SelectedText))]
    private int _selected = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTone), nameof(IsSilence))]
    private BleepSound _sound = BleepSound.Tone;

    /// <summary>The marked ranges, in order.</summary>
    [ObservableProperty] private IReadOnlyList<TrimRange> _ranges = [];

    public bool IsTone => Sound == BleepSound.Tone;
    public bool IsSilence => Sound == BleepSound.Silence;

    /// <summary>"0:01.2 – 0:02.0" for the picked range.</summary>
    public string SelectedText => Selected >= 0 && Selected < Ranges.Count
        ? $"{TrimMath.Format(Ranges[Selected].Start)} – {TrimMath.Format(Ranges[Selected].End)}"
        : "";

    protected override bool HasChanges => _marks.Ranges.Count > 0;

    protected override async Task LoadPreviewAsync()
    {
        Directory.CreateDirectory(_scratch);
        var summary = await MediaPreview.ProbeAsync(Source, CancellationToken.None);
        if (!summary.HasAudio)
            throw new NotSupportedException("The file has no sound.");
        Duration = summary.Duration;
        _marks = new BleepRanges(Duration);
        Peaks = await MediaPreview.PeaksAsync(Source, WaveformPeaks, Path.Combine(_scratch, "sound.pcm"), CancellationToken.None);
    }

    /// <summary>A range dragged from one time to another.</summary>
    public void Mark(double from, double to)
    {
        var before = _marks.Ranges.ToArray();
        var index = _marks.Add(from, to);
        if (index < 0)
            return;
        _undo.Push(before);
        Selected = index;
        Refresh();
    }

    /// <summary>A click picks the range under it, or none.</summary>
    public void Pick(double seconds) => Selected = _marks.At(seconds);

    [RelayCommand]
    private void SetSound(BleepSound sound) => Sound = sound;

    private bool HasSelection() => Selected >= 0;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        _undo.Push([.. _marks.Ranges]);
        _marks.Remove(Selected);
        Selected = -1;
        Refresh();
    }

    [RelayCommand]
    private void Clear()
    {
        if (_marks.Ranges.Count == 0)
            return;
        _undo.Push([.. _marks.Ranges]);
        _marks.Clear();
        Selected = -1;
        Refresh();
    }

    public override bool Undo()
    {
        if (_undo.Count == 0)
            return false;
        _marks = new BleepRanges(Duration);
        foreach (var range in _undo.Pop())
            _marks.Add(range.Start, range.End);
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

    public override void ApplyTo(Preset preset) => preset.Media.Edit = new MediaEdit { Bleeps = [.. _marks.Ranges], BleepSound = Sound };

    private void Refresh()
    {
        Ranges = [.. _marks.Ranges];
        OnPropertyChanged(nameof(SelectedText));
        ChangesChanged();
    }

    public override void Dispose()
    {
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
