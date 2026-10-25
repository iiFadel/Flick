// fork: see OrganizeEditorView.axaml. A press on a card picks it; letting go over another card moves it there.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Filee.App.Editors;

public partial class OrganizeEditorView : UserControl
{
    private int? _dragged;

    public OrganizeEditorView()
    {
        InitializeComponent();
        Grid.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (CardAt(e.Source) is not { } card || !e.GetCurrentPoint(Grid).Properties.IsLeftButtonPressed)
                return;
            _dragged = card.Position;
            (DataContext as OrganizeEditorViewModel)?.Select(card.Position);
            e.Pointer.Capture(Grid);
        }, handledEventsToo: true);
        Grid.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            e.Pointer.Capture(null);
            if (_dragged is not { } from)
                return;
            _dragged = null;
            var over = Grid.InputHitTest(e.GetPosition(Grid)) as Visual;
            if (CardAt(over) is { } target && target.Position != from)
                (DataContext as OrganizeEditorViewModel)?.Move(from, target.Position);
        }, handledEventsToo: true);
    }

    private static PageCard? CardAt(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors().OfType<StyledElement>().Select(v => v.DataContext).OfType<PageCard>().FirstOrDefault();
}

/// <summary>A page's turn in degrees as a transform for its picture.</summary>
public sealed class RotationTransformConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new RotateTransform(value is int degrees ? degrees : 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
