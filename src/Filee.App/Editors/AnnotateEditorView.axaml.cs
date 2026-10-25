// fork: see AnnotateEditorView.axaml. Builds the tool and colour buttons, keeps their "selected" look in step with the
// model, and passes the canvas's drags and clicks on.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public partial class AnnotateEditorView : UserControl
{
    private static readonly (AnnotationKind Kind, string Key)[] ToolKeys =
    [
        (AnnotationKind.Arrow, "editor.annotate.arrow"),
        (AnnotationKind.Rectangle, "editor.annotate.box"),
        (AnnotationKind.Ellipse, "editor.annotate.ellipse"),
        (AnnotationKind.Text, "editor.annotate.text"),
        (AnnotationKind.Callout, "editor.annotate.callout"),
    ];

    private AnnotateEditorViewModel? _model;

    public AnnotateEditorView()
    {
        InitializeComponent();
        foreach (var (kind, key) in ToolKeys)
        {
            var button = new Button { CommandParameter = kind };
            button.Classes.Add("editor-choice");
            button[!ContentProperty] = button.GetResourceObservable(key).ToBinding();
            Tools.Children.Add(button);
        }
        foreach (var color in AnnotateEditorViewModel.Colors)
        {
            var button = new Button { CommandParameter = color, Background = new SolidColorBrush(Color.Parse(color)) };
            button.Classes.Add("swatch");
            Swatches.Children.Add(button);
        }
        Canvas.Dragged += (from, to) => _model?.Drag(from.X, from.Y, to.X, to.Y);
        Canvas.Clicked += point => _model?.Click(point.X, point.Y);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
            _model.PropertyChanged -= OnModelChanged;
        _model = DataContext as AnnotateEditorViewModel;
        if (_model is null)
            return;
        _model.PropertyChanged += OnModelChanged;
        foreach (var button in Tools.Children.OfType<Button>())
            button.Command = _model.SetToolCommand;
        foreach (var button in Swatches.Children.OfType<Button>())
            button.Command = _model.SetColorCommand;
        ShowChoices();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnnotateEditorViewModel.Tool) or nameof(AnnotateEditorViewModel.Color))
            ShowChoices();
    }

    private void ShowChoices()
    {
        foreach (var button in Tools.Children.OfType<Button>())
            button.Classes.Set("selected", Equals(button.CommandParameter, _model?.Tool));
        foreach (var button in Swatches.Children.OfType<Button>())
            button.Classes.Set("selected", Equals(button.CommandParameter, _model?.Color));
    }
}
