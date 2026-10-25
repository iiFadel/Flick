// fork: the Bleep editor's waveform (our design): the song's peaks in grey, each bleeped range as an orange band with the
// waveform inside it orange, the picked one outlined. Dragging marks a new range; a click picks the range under it.
// Times go out in seconds.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public sealed class BleepTimeline : Control
{
    public static readonly StyledProperty<double> DurationProperty = AvaloniaProperty.Register<BleepTimeline, double>(nameof(Duration));
    public static readonly StyledProperty<float[]> PeaksProperty = AvaloniaProperty.Register<BleepTimeline, float[]>(nameof(Peaks), []);
    public static readonly StyledProperty<IReadOnlyList<TrimRange>> RangesProperty =
        AvaloniaProperty.Register<BleepTimeline, IReadOnlyList<TrimRange>>(nameof(Ranges), []);
    public static readonly StyledProperty<int> SelectedProperty = AvaloniaProperty.Register<BleepTimeline, int>(nameof(Selected), -1);
    public static readonly StyledProperty<IBrush?> AccentProperty = AvaloniaProperty.Register<BleepTimeline, IBrush?>(nameof(Accent), Brushes.OrangeRed);
    public static readonly StyledProperty<IBrush?> WaveProperty = AvaloniaProperty.Register<BleepTimeline, IBrush?>(nameof(Wave), Brushes.Gray);

    private double? _from;
    private double _to;

    static BleepTimeline()
    {
        AffectsRender<BleepTimeline>(DurationProperty, PeaksProperty, RangesProperty, SelectedProperty, AccentProperty, WaveProperty);
        CursorProperty.OverrideDefaultValue<BleepTimeline>(new Cursor(StandardCursorType.Ibeam));
    }

    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public float[] Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public IReadOnlyList<TrimRange> Ranges { get => GetValue(RangesProperty); set => SetValue(RangesProperty, value); }
    public int Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public IBrush? Wave { get => GetValue(WaveProperty); set => SetValue(WaveProperty, value); }

    /// <summary>A range dragged from one time to another (seconds).</summary>
    public event Action<double, double>? Marked;

    /// <summary>A click at a time (seconds).</summary>
    public event Action<double>? Clicked;

    private double X(double seconds) => Duration > 0 ? Bounds.Width * seconds / Duration : 0;

    private double Seconds(double x) => Duration * Math.Clamp(x / Math.Max(1, Bounds.Width), 0, 1);

    public override void Render(DrawingContext context)
    {
        if (Duration <= 0)
            return;
        var accent = Accent ?? Brushes.OrangeRed;
        var band = new SolidColorBrush(Colors.OrangeRed, 0.16);
        var ranges = Ranges.ToList();
        if (_from is { } from)
            ranges.Add(new TrimRange(Math.Min(from, _to), Math.Max(from, _to)));
        foreach (var range in ranges)
            context.FillRectangle(band, new Rect(X(range.Start), 0, Math.Max(1, X(range.End) - X(range.Start)), Bounds.Height));

        var middle = Bounds.Height / 2;
        var bar = Bounds.Width / Math.Max(1, Peaks.Length);
        for (var i = 0; i < Peaks.Length; i++)
        {
            var seconds = (i + 0.5) / Peaks.Length * Duration;
            var lit = ranges.Any(r => seconds >= r.Start && seconds <= r.End);
            var height = Math.Max(1, Peaks[i] * (Bounds.Height - 16));
            context.FillRectangle(lit ? accent : Wave ?? Brushes.Gray, new Rect(i * bar, middle - height / 2, Math.Max(1, bar - 0.5), height));
        }
        if (Selected >= 0 && Selected < Ranges.Count)
        {
            var range = Ranges[Selected];
            context.DrawRectangle(null, new Pen(accent, 2), new RoundedRect(new Rect(X(range.Start), 1, Math.Max(2, X(range.End) - X(range.Start)), Bounds.Height - 2), 4));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Duration <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _from = _to = Seconds(e.GetPosition(this).X);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_from is null)
            return;
        _to = Seconds(e.GetPosition(this).X);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_from is not { } from)
            return;
        _from = null;
        e.Pointer.Capture(null);
        var to = Seconds(e.GetPosition(this).X);
        if (Math.Abs(to - from) >= BleepRanges.MinLength)
            Marked?.Invoke(from, to);
        else
            Clicked?.Invoke(to);
        InvalidateVisual();
    }
}
