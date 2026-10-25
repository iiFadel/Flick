// fork: the Annotate editor's canvas (our design): the picture fitted in, every mark drawn at the sizes the PNG will have
// (AnnotationGeometry, scaled to the picture on screen), the picked one with a dashed orange frame. Dragging draws an
// arrow, box or ellipse; a click places text or a callout, or picks the mark under it. Points go out as fractions of
// the picture.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public sealed class AnnotationCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> PictureProperty = AvaloniaProperty.Register<AnnotationCanvas, Bitmap?>(nameof(Picture));
    public static readonly StyledProperty<IReadOnlyList<(Annotation Mark, int Number)>> MarksProperty =
        AvaloniaProperty.Register<AnnotationCanvas, IReadOnlyList<(Annotation Mark, int Number)>>(nameof(Marks), []);
    public static readonly StyledProperty<int> SelectedProperty = AvaloniaProperty.Register<AnnotationCanvas, int>(nameof(Selected), -1);
    public static readonly StyledProperty<AnnotationKind> ToolProperty = AvaloniaProperty.Register<AnnotationCanvas, AnnotationKind>(nameof(Tool));
    public static readonly StyledProperty<string> ColorProperty = AvaloniaProperty.Register<AnnotationCanvas, string>(nameof(Color), "#FF5200");
    public static readonly StyledProperty<IBrush?> AccentProperty = AvaloniaProperty.Register<AnnotationCanvas, IBrush?>(nameof(Accent), Brushes.OrangeRed);

    private Point? _from;
    private Point _to;

    static AnnotationCanvas()
    {
        AffectsRender<AnnotationCanvas>(PictureProperty, MarksProperty, SelectedProperty, ToolProperty, ColorProperty, AccentProperty);
        CursorProperty.OverrideDefaultValue<AnnotationCanvas>(new Cursor(StandardCursorType.Cross));
    }

    public Bitmap? Picture { get => GetValue(PictureProperty); set => SetValue(PictureProperty, value); }
    public IReadOnlyList<(Annotation Mark, int Number)> Marks { get => GetValue(MarksProperty); set => SetValue(MarksProperty, value); }
    public int Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    public AnnotationKind Tool { get => GetValue(ToolProperty); set => SetValue(ToolProperty, value); }
    public string Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>A drag from one point to another (fractions of the picture).</summary>
    public event Action<Point, Point>? Dragged;

    /// <summary>A click at a point (fractions of the picture).</summary>
    public event Action<Point>? Clicked;

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

    public override void Render(DrawingContext context)
    {
        var area = Fitted();
        if (Picture is not { } picture || area.Width <= 0)
            return;
        context.DrawImage(picture, new Rect(picture.Size), area);
        using (context.PushClip(area))
        {
            for (var i = 0; i < Marks.Count; i++)
                DrawMark(context, area, Marks[i].Mark, Marks[i].Number, i == Selected);
            if (_from is { } from && Tool is not (AnnotationKind.Text or AnnotationKind.Callout))
            {
                var (a, b) = (Fraction(from), Fraction(_to));
                DrawMark(context, area, new Annotation(Tool, a.X, a.Y, b.X, b.Y, Color), 0, false);
            }
        }
    }

    private void DrawMark(DrawingContext context, Rect area, Annotation mark, int number, bool selected)
    {
        var side = Math.Min(area.Width, area.Height);
        var brush = new SolidColorBrush(Avalonia.Media.Color.Parse(mark.Color));
        var pen = new Pen(brush, Math.Max(1.5, side * AnnotationGeometry.Stroke), lineCap: PenLineCap.Round);
        var p1 = new Point(area.X + mark.X1 * area.Width, area.Y + mark.Y1 * area.Height);
        var p2 = new Point(area.X + mark.X2 * area.Width, area.Y + mark.Y2 * area.Height);
        Rect bounds;
        switch (mark.Kind)
        {
            case AnnotationKind.Arrow:
                var (left, right) = AnnotationGeometry.ArrowHead(p1.X, p1.Y, p2.X, p2.Y, AnnotationGeometry.HeadSize(side));
                context.DrawLine(pen, p1, new Point((left.X + right.X) / 2, (left.Y + right.Y) / 2));
                var head = new StreamGeometry();
                using (var g = head.Open())
                {
                    g.BeginFigure(p2, true);
                    g.LineTo(new Point(left.X, left.Y));
                    g.LineTo(new Point(right.X, right.Y));
                    g.EndFigure(true);
                }
                context.DrawGeometry(brush, null, head);
                bounds = new Rect(p1, p2).Normalize();
                break;
            case AnnotationKind.Rectangle:
                bounds = new Rect(p1, p2).Normalize();
                context.DrawRectangle(null, pen, new RoundedRect(bounds, pen.Thickness * 1.5));
                break;
            case AnnotationKind.Ellipse:
                bounds = new Rect(p1, p2).Normalize();
                context.DrawEllipse(null, pen, bounds);
                break;
            case AnnotationKind.Text:
                var text = new FormattedText(mark.Text.Length > 0 ? mark.Text : " ", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Arial"), side * AnnotationGeometry.TextSize, brush);
                context.DrawText(text, p1);
                bounds = new Rect(p1, new Size(Math.Max(8, text.Width), text.Height));
                break;
            default:
                var radius = side * AnnotationGeometry.CalloutRadius;
                context.DrawEllipse(brush, new Pen(Brushes.White, Math.Max(1.5, pen.Thickness * 0.6)), p1, radius, radius);
                var digits = new FormattedText(number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Arial", FontStyle.Normal, FontWeight.Bold), radius * 1.15, Brushes.White);
                context.DrawText(digits, new Point(p1.X - digits.Width / 2, p1.Y - digits.Height / 2));
                bounds = new Rect(p1.X - radius, p1.Y - radius, radius * 2, radius * 2);
                break;
        }
        if (selected)
            context.DrawRectangle(null, new Pen(Accent ?? Brushes.OrangeRed, 1.5, new DashStyle([4, 3], 0)), bounds.Inflate(6));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Fitted().Width <= 0)
            return;
        Focus();
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
        var dragged = Math.Abs(end.X - start.X) >= AnnotationDocument.MinDrag || Math.Abs(end.Y - start.Y) >= AnnotationDocument.MinDrag;
        if (dragged && Tool is not (AnnotationKind.Text or AnnotationKind.Callout))
            Dragged?.Invoke(start, end);
        else
            Clicked?.Invoke(end);
        InvalidateVisual();
    }
}
