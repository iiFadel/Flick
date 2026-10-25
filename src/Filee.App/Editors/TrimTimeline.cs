// fork: the Trim editor's timeline (our design): small frames side by side (video) or the waveform in orange (audio),
// what is cut off dimmed, the part kept framed in orange with a round handle at each end. Dragging near a handle moves
// it; pressing elsewhere brings the nearer handle there.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public sealed class TrimTimeline : Control
{
    public static readonly StyledProperty<double> DurationProperty = AvaloniaProperty.Register<TrimTimeline, double>(nameof(Duration));
    public static readonly StyledProperty<TrimRange> RangeProperty = AvaloniaProperty.Register<TrimTimeline, TrimRange>(nameof(Range));
    public static readonly StyledProperty<IReadOnlyList<Bitmap>> FramesProperty =
        AvaloniaProperty.Register<TrimTimeline, IReadOnlyList<Bitmap>>(nameof(Frames), []);
    public static readonly StyledProperty<float[]> PeaksProperty = AvaloniaProperty.Register<TrimTimeline, float[]>(nameof(Peaks), []);
    public static readonly StyledProperty<TrimHandle> SelectedProperty = AvaloniaProperty.Register<TrimTimeline, TrimHandle>(nameof(Selected));
    public static readonly StyledProperty<IBrush?> AccentProperty = AvaloniaProperty.Register<TrimTimeline, IBrush?>(nameof(Accent), Brushes.OrangeRed);
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<TrimTimeline, IBrush?>(nameof(Track), Brushes.LightGray);

    private const double HandleWidth = 12;
    private const double Radius = 8;
    private static readonly IBrush Dim = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    private TrimHandle? _dragging;

    static TrimTimeline()
    {
        AffectsRender<TrimTimeline>(DurationProperty, RangeProperty, FramesProperty, PeaksProperty, SelectedProperty, AccentProperty, TrackProperty);
        FocusableProperty.OverrideDefaultValue<TrimTimeline>(true);
    }

    public double Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public TrimRange Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
    public IReadOnlyList<Bitmap> Frames { get => GetValue(FramesProperty); set => SetValue(FramesProperty, value); }
    public float[] Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public TrimHandle Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    /// <summary>A handle was dragged to a time (seconds).</summary>
    public event Action<TrimHandle, double>? HandleMoved;

    /// <summary>The strip between the handles' outer edges.</summary>
    private Rect Strip => new(HandleWidth, 0, Math.Max(1, Bounds.Width - 2 * HandleWidth), Bounds.Height);

    private double X(double seconds) => Strip.X + Strip.Width * (Duration > 0 ? seconds / Duration : 0);

    private double Seconds(double x) => Duration * Math.Clamp((x - Strip.X) / Strip.Width, 0, 1);

    public override void Render(DrawingContext context)
    {
        var strip = Strip;
        using (context.PushClip(new RoundedRect(strip, Radius)))
        {
            context.FillRectangle(Track ?? Brushes.LightGray, strip);
            if (Frames.Count > 0)
            {
                var width = strip.Width / Frames.Count;
                for (var i = 0; i < Frames.Count; i++)
                {
                    var frame = Frames[i];
                    var cell = new Rect(strip.X + i * width, strip.Y, width + 0.5, strip.Height);
                    // Fill the cell, cutting the frame's sides as needed.
                    var scale = Math.Max(cell.Width / frame.Size.Width, cell.Height / frame.Size.Height);
                    var shown = new Size(cell.Width / scale, cell.Height / scale);
                    var from = new Rect((frame.Size.Width - shown.Width) / 2, (frame.Size.Height - shown.Height) / 2, shown.Width, shown.Height);
                    context.DrawImage(frame, from, cell);
                }
            }
            else if (Peaks.Length > 0)
            {
                var middle = strip.Center.Y;
                var bar = strip.Width / Peaks.Length;
                for (var i = 0; i < Peaks.Length; i++)
                {
                    var height = Math.Max(1, Peaks[i] * (strip.Height - 12));
                    context.FillRectangle(Accent ?? Brushes.OrangeRed, new Rect(strip.X + i * bar, middle - height / 2, Math.Max(1, bar - 0.5), height));
                }
            }
            var (start, end) = (X(Range.Start), X(Range.End));
            context.FillRectangle(Dim, new Rect(strip.X, 0, Math.Max(0, start - strip.X), strip.Height));
            context.FillRectangle(Dim, new Rect(end, 0, Math.Max(0, strip.Right - end), strip.Height));
        }

        if (Duration <= 0)
            return;
        var accent = Accent ?? Brushes.OrangeRed;
        var (left, right) = (X(Range.Start), X(Range.End));
        var frameRect = new Rect(left - HandleWidth, 0, right - left + 2 * HandleWidth, Bounds.Height);
        context.DrawRectangle(null, new Pen(accent, 3), new RoundedRect(frameRect.Deflate(1.5), Radius));
        DrawHandle(context, new Rect(left - HandleWidth, 0, HandleWidth, Bounds.Height), accent, Selected == TrimHandle.Start);
        DrawHandle(context, new Rect(right, 0, HandleWidth, Bounds.Height), accent, Selected == TrimHandle.End);
    }

    private static void DrawHandle(DrawingContext context, Rect rect, IBrush accent, bool selected)
    {
        context.DrawRectangle(accent, null, new RoundedRect(rect, Radius));
        var grip = new Rect(rect.Center.X - 1.5, rect.Center.Y - 10, 3, 20);
        context.DrawRectangle(selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), null, new RoundedRect(grip, 1.5));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Duration <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        Focus();
        var x = e.GetPosition(this).X;
        _dragging = Math.Abs(x - X(Range.Start)) <= Math.Abs(x - X(Range.End)) ? TrimHandle.Start : TrimHandle.End;
        e.Pointer.Capture(this);
        HandleMoved?.Invoke(_dragging.Value, Seconds(x));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging is { } handle)
            HandleMoved?.Invoke(handle, Seconds(e.GetPosition(this).X));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = null;
        e.Pointer.Capture(null);
    }
}
