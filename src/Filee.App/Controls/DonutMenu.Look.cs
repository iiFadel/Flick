// fork: Tangerine's look for the donut (docs/TANGERINE-SPEC.md). A frosted track behind petals with a constant gap and
// rounded corners (PetalGeometry); format names, or a line icon over a small label; and a pill in the hole naming the
// selected slice. Render draws this instead of RenderRing and DrawCenter. Groups' rings, the edit mode's preview and
// the progress ring keep their own drawing, coloured through the same theme keys (Styles/Flick.axaml).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Filee.App.Controls;

public sealed partial class DonutMenu
{
    public static readonly StyledProperty<IBrush?> WheelTrackBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(WheelTrackBrush), new SolidColorBrush(Color.Parse("#F0D6DADB")));
    public static readonly StyledProperty<IBrush?> PetalFillProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(PetalFill), Brushes.WhiteSmoke);
    public static readonly StyledProperty<IBrush?> PetalStrokeBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(PetalStrokeBrush));
    public static readonly StyledProperty<IBrush?> PetalSelectedStrokeBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(PetalSelectedStrokeBrush));
    public static readonly StyledProperty<Color> WheelShadowColorProperty =
        AvaloniaProperty.Register<DonutMenu, Color>(nameof(WheelShadowColor), Color.Parse("#25263438"));
    public static readonly StyledProperty<IBrush?> WheelLabelBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(WheelLabelBrush), Brushes.Black);
    public static readonly StyledProperty<IBrush?> CenterPillBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(CenterPillBrush), Brushes.White);
    public static readonly StyledProperty<IBrush?> CenterPillBorderBrushProperty =
        AvaloniaProperty.Register<DonutMenu, IBrush?>(nameof(CenterPillBorderBrush));
    public static readonly StyledProperty<BoxShadows> CenterPillShadowProperty =
        AvaloniaProperty.Register<DonutMenu, BoxShadows>(nameof(CenterPillShadow));
    public static readonly StyledProperty<FontFamily> WheelFontFamilyProperty =
        AvaloniaProperty.Register<DonutMenu, FontFamily>(nameof(WheelFontFamily), FontFamily.Default);

    /// <summary>Called from the static constructor.</summary>
    private static void RegisterLook() =>
        AffectsRender<DonutMenu>(WheelTrackBrushProperty, PetalFillProperty, PetalStrokeBrushProperty,
            PetalSelectedStrokeBrushProperty, WheelShadowColorProperty, WheelLabelBrushProperty, CenterPillBrushProperty,
            CenterPillBorderBrushProperty, CenterPillShadowProperty, WheelFontFamilyProperty);

    public IBrush? WheelTrackBrush { get => GetValue(WheelTrackBrushProperty); set => SetValue(WheelTrackBrushProperty, value); }
    public IBrush? PetalFill { get => GetValue(PetalFillProperty); set => SetValue(PetalFillProperty, value); }
    public IBrush? PetalStrokeBrush { get => GetValue(PetalStrokeBrushProperty); set => SetValue(PetalStrokeBrushProperty, value); }
    public IBrush? PetalSelectedStrokeBrush { get => GetValue(PetalSelectedStrokeBrushProperty); set => SetValue(PetalSelectedStrokeBrushProperty, value); }
    public Color WheelShadowColor { get => GetValue(WheelShadowColorProperty); set => SetValue(WheelShadowColorProperty, value); }
    public IBrush? WheelLabelBrush { get => GetValue(WheelLabelBrushProperty); set => SetValue(WheelLabelBrushProperty, value); }
    public IBrush? CenterPillBrush { get => GetValue(CenterPillBrushProperty); set => SetValue(CenterPillBrushProperty, value); }
    public IBrush? CenterPillBorderBrush { get => GetValue(CenterPillBorderBrushProperty); set => SetValue(CenterPillBorderBrushProperty, value); }
    public BoxShadows CenterPillShadow { get => GetValue(CenterPillShadowProperty); set => SetValue(CenterPillShadowProperty, value); }
    public FontFamily WheelFontFamily { get => GetValue(WheelFontFamilyProperty); set => SetValue(WheelFontFamilyProperty, value); }

    /// <summary>The wheel's radii: Tangerine's proportions at this donut's size.</summary>
    internal PetalLayout Layout => PetalLayout.ForOuter(OuterRadius);

    /// <summary>This wheel's size against Tangerine's: sizes from the spec are multiplied by it.</summary>
    private double LookScale => OuterRadius / PetalLayout.SiteOuter;

    // Spec sizes (docs/TANGERINE-SPEC.md), at Tangerine's own size.
    private const double FormatLabelSize = 14, FormatLetterSpacing = 0.35;
    private const double ToolLabelSize = 8.5, ToolLetterSpacing = 0.2, ToolIconSize = 18;
    private const double ToolIconAbove = 7.5, ToolLabelBelow = 10.5;
    private const double PillWidth = 70, PillHeight = 32, ToolPillHeight = 36, PillTextSize = 12;
    private const double MinFormatLabelSize = 9;

    /// <summary>The soft light on the selected petal in the recording (our design: the site's orange is flat).</summary>
    private static readonly IBrush SelectedGlow = new RadialGradientBrush
    {
        GradientStops = { new GradientStop(Color.FromArgb(46, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) },
    };

    private static readonly IPen TrackHairline = new Pen(new SolidColorBrush(Colors.White, 0.33), 1);

    /// <summary>
    /// A gap between petals, a corner the rounding cut away, or the band just outside them selects nothing, as in
    /// Tangerine. While a group is about to open or open, the slices keep their full sectors, so the pointer crossing
    /// to its ring never folds it.
    /// </summary>
    private DonutHit WithGaps(DonutHit hit, Point p) =>
        hit.Kind == DonutHitKind.Segment && !IsEditMode && _expanded < 0 && _pendingGroup < 0 &&
        PetalGeometry.IndexAt(Center, p, Layout, Count) != hit.Index
            ? new DonutHit(DonutHitKind.Gap, hit.Index)
            : hit;

    /// <summary>How far the wheel has opened (0 to 1).</summary>
    internal double WheelOpen => _open.Length == 0 ? 1 : Math.Clamp(_open.Average(), 0, 1);

    /// <summary>How visible the labels and the pill are: they fade in a little after the wheel starts to bloom.</summary>
    internal double LabelOpacity => _openClock < double.MaxValue && !_closing ? WheelMotion.LabelAlpha(_openClock) : 1;

    /// <summary>Moves a petal's highlight straight towards <paramref name="target"/> in <see cref="WheelMotion.HoverSeconds"/>.</summary>
    private static bool Fade(ref Spring highlight, double target, double dt)
    {
        var step = dt / WheelMotion.HoverSeconds;
        highlight.Value = highlight.Value < target ? Math.Min(target, highlight.Value + step) : Math.Max(target, highlight.Value - step);
        highlight.Velocity = 0;
        return highlight.Value != target;
    }

    /// <summary>The shadow, the track, the petals and their labels.</summary>
    private void RenderWheel(DrawingContext context, Point center, int n)
    {
        var layout = Layout;
        var s = LookScale;
        var open = WheelOpen;
        var bloom = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(0.45 + 0.55 * open, 0.45 + 0.55 * open) *
                    Matrix.CreateTranslation(center.X, center.Y);

        using var _ = context.PushTransform(bloom);
        using var __ = context.PushOpacity(open);

        if (n == 0 && !IsEditMode && !string.IsNullOrEmpty(CenterSubtitle))
        {
            DrawEmptyCard(context, center, CenterSubtitle, s);
            return;
        }

        DrawWheelShadow(context, center, layout, s);
        context.DrawGeometry(WheelTrackBrush, null, Annulus(center, layout.Hole, layout.Backdrop));
        context.DrawEllipse(null, TrackHairline, center, layout.Backdrop - 0.5, layout.Backdrop - 0.5);

        var stroke = PetalStrokeBrush is null ? null : new Pen(PetalStrokeBrush, 0.75);
        var selectedStroke = PetalSelectedStrokeBrush is null ? null : new Pen(PetalSelectedStrokeBrush, 0.75);

        if (n == 0 && IsEditMode)
        {
            // The editor's empty toolbar: one ring to drop the first preset on.
            var ring = Annulus(center, layout.Inner, layout.Outer);
            context.DrawGeometry(PetalFill, stroke, ring);
            if (_dropTargetIndex >= 0)
                context.DrawGeometry(WithOpacity(AccentBrush, 0.35), null, ring);
        }

        // SF Pro Rounded at 600 reads as Nunito at 700.
        var bold = new Typeface(WheelFontFamily, FontStyle.Normal, FontWeight.Bold);
        for (var i = 0; i < n; i++)
        {
            var item = Items![i];
            var previewing = IsEditMode && i < _mid.Length;
            var mid = previewing ? _mid[i].Value : DonutGeometry.MidAngle(i, n);
            var span = previewing ? _span[i].Value : DonutGeometry.Span(n);
            if (span < 0.01)
                continue;

            var petal = PetalShape(center, layout, mid, span);
            using (context.PushOpacity(i == _dimmedIndex ? 0.3 : 1))
            {
                using (context.PushOpacity(SliceOpacity * (item.IsEnabled ? 1 : 0.55)))
                    context.DrawGeometry(PetalFill, stroke, petal);

                var hover = i < _hover.Length && _previewSlot < 0 ? _hover[i].Value : 0;
                var emphasis = Math.Clamp(i == _dropTargetIndex ? 1.0 : hover, 0, 1);
                if (emphasis > 0.01 && item.IsEnabled)
                {
                    using (context.PushOpacity(emphasis))
                    {
                        context.DrawGeometry(AccentBrush, selectedStroke, petal);
                        context.DrawGeometry(SelectedGlow, null, petal);
                    }
                }

                var hot = (i == ActiveIndex || i == _dropTargetIndex) && item.IsEnabled;
                using var labels = context.PushOpacity(LabelOpacity);
                DrawPetalLabel(context, item, DonutGeometry.PointAt(center, layout.LabelRadius, mid), mid, span, layout, s, bold, hot);
            }
        }

        if (IsEditMode && _previewSpan.Value > 0.01)
            DrawPreview(context, center, layout.Inner, layout.Outer, bold, new Typeface(GetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty)));
    }

    /// <summary>A soft shadow under the petals: (0, 5), blurred 8 px, faked with a radial gradient (no blur effects needed).</summary>
    private void DrawWheelShadow(DrawingContext context, Point center, PetalLayout layout, double s)
    {
        var blur = 8 * s;
        var radius = layout.Outer + blur;
        var color = WheelShadowColor;
        var clear = Color.FromArgb(0, color.R, color.G, color.B);
        var brush = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(clear, Math.Max(0, (layout.Inner - blur) / radius)),
                new GradientStop(color, (layout.Inner + blur / 2) / radius),
                new GradientStop(color, (layout.Outer - blur / 2) / radius),
                new GradientStop(clear, 1),
            },
        };
        context.DrawEllipse(brush, null, new Point(center.X, center.Y + 5 * s), radius, radius);
    }

    /// <summary>A format name, or a tool's icon over its name.</summary>
    private void DrawPetalLabel(DrawingContext context, DonutItem item, Point anchor, double mid, double span, PetalLayout layout,
        double s, Typeface bold, bool hot)
    {
        var brush = hot ? OnAccentBrush : item.IsEnabled ? WheelLabelBrush : SecondaryForeground;
        // Room across the petal at the label: its chord, less a margin; never wider than the band is deep.
        var room = Math.Min(2 * layout.LabelRadius * Math.Sin(Math.Min(span, Math.PI) / 2) - 8 * s, (layout.Outer - layout.Inner) * 1.2);

        if (item.Icon is { } icon)
        {
            DrawIcon(context, icon, new Point(anchor.X, anchor.Y - ToolIconAbove * s), ToolIconSize * s, brush);
            var title = WheelText(item.Label.ToUpper(System.Globalization.CultureInfo.CurrentUICulture), bold,
                ToolLabelSize * s, ToolLetterSpacing * s, brush, room, maxLines: 1);
            title.Draw(context, new Point(anchor.X - room / 2, anchor.Y + ToolLabelBelow * s - title.Height / 2));
            return;
        }

        // A word wider than the petal makes the name smaller (down to MinFormatLabelSize), so it never breaks inside a
        // word ("WEB|P"); names of several words wrap between them. Two pixels to spare: a word exactly as wide still wraps.
        var text = KeepKoreanWords(item.Label);
        var size = FormatLabelSize * s;
        var widest = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.Any(IsHanOrKana))
            .Select(word => NaturalWidth(word, bold, size, FormatLetterSpacing * s))
            .DefaultIfEmpty(0)
            .Max();
        if (widest > room - 2)
            size = Math.Max(MinFormatLabelSize * s, size * (room - 2) / widest);
        // More words than two lines hold ("Trace to SVG" on a thin petal): smaller still.
        while (size > MinFormatLabelSize * s && WheelText(text, bold, size, FormatLetterSpacing * s, brush, room, maxLines: 0).TextLines.Count > 2)
            size = Math.Max(MinFormatLabelSize * s, size * 0.9);
        var label = WheelText(text, bold, size, FormatLetterSpacing * s, brush, room, maxLines: 2);
        label.Draw(context, new Point(anchor.X - room / 2, anchor.Y - label.Height / 2));
    }

    /// <summary>A 24 × 24 line icon, <paramref name="size"/> wide, centred on <paramref name="at"/>.</summary>
    private static void DrawIcon(DrawingContext context, Geometry icon, Point at, double size, IBrush? brush)
    {
        var scale = size / 24;
        var pen = new Pen(brush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(at.X, at.Y);
        using (context.PushTransform(matrix))
            context.DrawGeometry(null, pen, icon);
    }

    /// <summary>The pill in the hole naming the selected slice (hidden while none is).</summary>
    private void DrawWheelCenter(DrawingContext context, Point center)
    {
        var s = LookScale;
        var open = WheelOpen;
        var item = ActiveItem();
        if (item is null)
        {
            DrawHoleHint(context, center, s);
            return;
        }

        // A greyed-out slice says why ("Coming soon"): tooltips don't show during a drag (our design).
        var reason = item.IsEnabled ? null : item.DisabledReason;
        var tool = item.Icon is not null && reason is null;
        var text = reason ?? (tool ? item.Label.ToUpper(System.Globalization.CultureInfo.CurrentUICulture) : item.Label);
        var width = PillWidth * s;
        var typeface = new Typeface(WheelFontFamily, FontStyle.Normal, tool ? FontWeight.Bold : FontWeight.SemiBold);
        var label = WheelText(text, typeface, (tool ? ToolLabelSize : PillTextSize) * s, (tool ? ToolLetterSpacing : FormatLetterSpacing) * s,
            WheelLabelBrush, width - 12 * s, maxLines: 2);
        var height = Math.Max((tool ? ToolPillHeight : PillHeight) * s, label.Height + 8 * s);
        var box = new Rect(center.X - width / 2, center.Y - height / 2, width, height);

        using (context.PushOpacity(open * LabelOpacity))
        {
            var border = CenterPillBorderBrush is null ? null : new Pen(CenterPillBorderBrush, 1);
            context.DrawRectangle(CenterPillBrush, border, new RoundedRect(box, height / 2), CenterPillShadow);
            label.Draw(context, new Point(center.X - (width - 12 * s) / 2, center.Y - label.Height / 2));
        }
    }

    /// <summary>The slice the pill names: the hovered one of an open group's ring, else the emphasised slice.</summary>
    private DonutItem? ActiveItem()
    {
        if (_subHover >= 0 && _expanded >= 0 && _expanded < Count && Items![_expanded].Children is { } children && _subHover < children.Count)
            return children[_subHover];
        var index = ActiveIndex;
        return index >= 0 && index < Count ? Items![index] : null;
    }

    /// <summary>
    /// With nothing selected the hole stays empty, as in Tangerine; only the click mode's "Tab ↻ back" (our design) and
    /// a donut with nothing to show say something there, small.
    /// </summary>
    private void DrawHoleHint(DrawingContext context, Point center, double s)
    {
        var message = Count == 0 ? null : CenterHint; // an empty wheel says it on its card
        if (string.IsNullOrEmpty(message))
            return;
        var room = Layout.Hole * 1.6;
        var text = WheelText(message, new Typeface(WheelFontFamily, FontStyle.Normal, FontWeight.SemiBold), 10 * s, 0,
            SecondaryForeground, room, maxLines: 3);
        text.Draw(context, new Point(center.X - room / 2, center.Y - text.Height / 2));
    }

    private readonly Dictionary<(string Text, Typeface Typeface, double Size), double> _naturalWidths = new();

    /// <summary>Width of <paramref name="text"/> on one line, without wrapping or trimming.</summary>
    private double NaturalWidth(string text, Typeface typeface, double size, double letterSpacing)
    {
        var key = (text, typeface, size);
        if (_naturalWidths.TryGetValue(key, out var width))
            return width;
        if (_naturalWidths.Count >= MaxCachedWedges)
            _naturalWidths.Clear();
        using var line = new TextLayout(text, typeface, size, null, letterSpacing: letterSpacing);
        return _naturalWidths[key] = line.WidthIncludingTrailingWhitespace;
    }

    // The card an empty wheel shows (site: 245 wide, padding 24, radius 18, 13 px text at line height 1.7).
    private const double CardWidth = 245, CardPadding = 24, CardRadius = 18, CardTextSize = 13, CardLineHeight = 1.7;

    /// <summary>What an empty wheel shows instead of its ring: why it is empty, and the keys of the other wheels.</summary>
    private void DrawEmptyCard(DrawingContext context, Point center, string message, double s)
    {
        IBrush? Find(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
        var shadow = this.TryFindResource("WheelCardShadow", ActualThemeVariant, out var found) && found is BoxShadows shadows
            ? shadows : default;

        var width = CardWidth * s;
        var room = width - 2 * CardPadding * s;
        var size = CardTextSize * s;
        var text = new TextLayout(message, new Typeface(GetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty)), size,
            Find("WheelCardTextBrush") ?? SecondaryForeground, TextAlignment.Center, TextWrapping.Wrap, maxWidth: room,
            lineHeight: size * CardLineHeight);
        var height = text.Height + 2 * CardPadding * s;
        var box = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
        var border = Find("WheelCardBorderBrush") is { } brush ? new Pen(brush, 1) : null;
        context.DrawRectangle(Find("WheelCardBrush") ?? Brushes.White, border, new RoundedRect(box, CardRadius * s), shadow);
        text.Draw(context, new Point(box.X + CardPadding * s, box.Y + CardPadding * s));
    }

    private static TextLayout WheelText(string text, Typeface typeface, double size, double letterSpacing, IBrush? brush,
        double maxWidth, int maxLines) =>
        new(text, typeface, size, brush, TextAlignment.Center, TextWrapping.Wrap, TextTrimming.CharacterEllipsis,
            maxWidth: Math.Max(10, maxWidth), letterSpacing: letterSpacing, maxLines: maxLines);

    // Petals and the track only change with the size, the centre or the editor's sliding layout: they are kept.
    private readonly Dictionary<(Point C, double Outer, double Mid, double Span), StreamGeometry> _petals = new();
    private readonly Dictionary<(Point C, double R1, double R2), StreamGeometry> _annuli = new();

    private StreamGeometry PetalShape(Point center, PetalLayout layout, double mid, double span)
    {
        var key = (center, layout.Outer, mid, span);
        if (_petals.TryGetValue(key, out var cached))
            return cached;
        if (_petals.Count >= MaxCachedWedges)
            _petals.Clear();
        return _petals[key] = BuildPetal(PetalGeometry.Petal(center, layout, mid, span));
    }

    private StreamGeometry Annulus(Point center, double r1, double r2)
    {
        var key = (center, r1, r2);
        if (_annuli.TryGetValue(key, out var cached))
            return cached;
        if (_annuli.Count >= 16)
            _annuli.Clear();
        return _annuli[key] = CreateRing(center, r1, r2);
    }

    private static StreamGeometry BuildPetal(PetalOutline outline)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(outline.Start, true);
        foreach (var step in outline.Steps)
        {
            switch (step.Kind)
            {
                case PetalStepKind.Quad:
                    ctx.QuadraticBezierTo(step.Control, step.To);
                    break;
                case PetalStepKind.Arc:
                    ctx.ArcTo(step.To, new Size(step.ArcRadius, step.ArcRadius), 0, step.IsLargeArc,
                        step.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                    break;
                default:
                    ctx.LineTo(step.To);
                    break;
            }
        }
        ctx.EndFigure(true);
        return geometry;
    }
}
