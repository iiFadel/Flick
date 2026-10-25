// fork: the Redact editor's canvas (our design): the picture fitted in, each box drawn over it (black for Solid, frosted
// white with an orange edge for Blur), the picked one framed in orange. Dragging draws a new box; a click picks the box
// under it. Points go out as fractions of the picture.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public sealed class RegionCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> PictureProperty = AvaloniaProperty.Register<RegionCanvas, Bitmap?>(nameof(Picture));
    public static readonly StyledProperty<IReadOnlyList<NormalizedRect>> BoxesProperty =
        AvaloniaProperty.Register<RegionCanvas, IReadOnlyList<NormalizedRect>>(nameof(Boxes), []);
    public static readonly StyledProperty<int> SelectedProperty = AvaloniaProperty.Register<RegionCanvas, int>(nameof(Selected), -1);
    public static readonly StyledProperty<RedactStyle> StyleProperty = AvaloniaProperty.Register<RegionCanvas, RedactStyle>(nameof(Style));
    public static readonly StyledProperty<IBrush?> AccentProperty = AvaloniaProperty.Register<RegionCanvas, IBrush?>(nameof(Accent), Brushes.OrangeRed);

    private static readonly IBrush Frost = new SolidColorBrush(Color.FromArgb(200, 235, 236, 236));
    private Point? _from;
    private Point _to;

    static RegionCanvas()
    {
        AffectsRender<RegionCanvas>(PictureProperty, BoxesProperty, SelectedProperty, StyleProperty, AccentProperty);
        CursorProperty.OverrideDefaultValue<RegionCanvas>(new Cursor(StandardCursorType.Cross));
    }

    public Bitmap? Picture { get => GetValue(PictureProperty); set => SetValue(PictureProperty, value); }
    public IReadOnlyList<NormalizedRect> Boxes { get => GetValue(BoxesProperty); set => SetValue(BoxesProperty, value); }
    public int Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public RedactStyle Style { get => GetValue(StyleProperty); set => SetValue(StyleProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>A box was dragged from one corner to the other (fractions of the picture).</summary>
    public event Action<Point, Point>? Drawn;

    /// <summary>A click at a point (fractions of the picture).</summary>
    public event Action<Point>? Clicked;

    /// <summary>Where the picture is drawn, fitted in and centred.</summary>
    private Rect Fitted()
    {
        if (Picture is not { } picture || picture.Size.Width <= 0 || picture.Size.Height <= 0)
            return default;
        var scale = Math.Min(Bounds.Width / picture.Size.Width, Bounds.Height / picture.Size.Height);
        var size = new Size(picture.Size.Width * scale, picture.Size.Height * scale);
        return new Rect((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2, size.Width, size.Height);
    }

    private Point Fraction(Point point)
    {
        var area = Fitted();
        return area.Width <= 0 ? default : new Point((point.X - area.X) / area.Width, (point.Y - area.Y) / area.Height);
    }

    private static Rect OnScreen(NormalizedRect box, Rect area) =>
        new(area.X + box.X * area.Width, area.Y + box.Y * area.Height, box.Width * area.Width, box.Height * area.Height);

    public override void Render(DrawingContext context)
    {
        var area = Fitted();
        if (Picture is not { } picture || area.Width <= 0)
            return;
        context.DrawImage(picture, new Rect(picture.Size), area);
        var accent = Accent ?? Brushes.OrangeRed;
        for (var i = 0; i < Boxes.Count; i++)
        {
            var rect = OnScreen(Boxes[i], area);
            context.FillRectangle(Style == RedactStyle.Solid ? Brushes.Black : Frost, rect);
            if (i == Selected)
                context.DrawRectangle(null, new Pen(accent, 2.5), rect.Inflate(1.5));
            else if (Style == RedactStyle.Blur)
                context.DrawRectangle(null, new Pen(accent, 1), rect);
        }
        if (_from is { } from)
        {
            var drag = new Rect(from, _to).Normalize().Intersect(area);
            context.FillRectangle(new SolidColorBrush(Colors.Black, 0.35), drag);
            context.DrawRectangle(null, new Pen(accent, 1.5, new DashStyle([4, 3], 0)), drag);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Fitted().Width <= 0)
            return;
        _from = _to = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_from is null)
            return;
        _to = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_from is not { } from)
            return;
        _from = null;
        e.Pointer.Capture(null);
        var (start, end) = (Fraction(from), Fraction(e.GetPosition(this)));
        if (RedactRegions.Box(start.X, start.Y, end.X, end.Y) is null)
            Clicked?.Invoke(end);
        else
            Drawn?.Invoke(start, end);
        InvalidateVisual();
    }
}
