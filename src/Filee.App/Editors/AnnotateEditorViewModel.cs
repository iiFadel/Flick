// fork: the Annotate editor (Phase 5): pick Arrow, Box, Ellipse, Text or Callout and a colour, then drag (arrow, box,
// ellipse) or click (text, callout) on the picture. A click on a mark with a drawing tool picks it: its colour and text
// can be changed, Delete removes it. Ctrl+Z / Ctrl+Y undo and redo. Apply draws the marks through the image engine
// into "name Annotated.png" (always PNG, as Tangerine).

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public sealed partial class AnnotateEditorViewModel(string title, string source, ILogger? log, string newText) : EditorViewModel(title, source, log)
{
    /// <summary>The colours offered: Flick's orange, red, yellow, green, blue, black and white.</summary>
    public static readonly string[] Colors = ["#FF5200", "#E5484D", "#FFC53D", "#30A46C", "#0090FF", "#111111", "#FFFFFF"];

    private readonly AnnotationDocument _document = new();
    private int _width;
    private int _height;

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private IReadOnlyList<(Annotation Mark, int Number)> _marks = [];
    [ObservableProperty] private AnnotationKind _tool = AnnotationKind.Arrow;
    [ObservableProperty] private string _color = Colors[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIsText))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private int _selected = -1;

    /// <summary>The text of the picked text mark, as typed.</summary>
    [ObservableProperty] private string _text = "";

    public bool SelectedIsText => Selected >= 0 && Selected < _document.Marks.Count && _document.Marks[Selected].Kind == AnnotationKind.Text;

    protected override bool HasChanges => _document.Marks.Count > 0;

    protected override async Task LoadPreviewAsync()
    {
        (Preview, _width, _height) = await CropService.LoadPreviewAsync(Source);
    }

    [RelayCommand]
    private void SetTool(AnnotationKind tool) => Tool = tool;

    /// <summary>Picks the colour for new marks, and gives it to the picked one.</summary>
    [RelayCommand]
    private void SetColor(string color)
    {
        Color = color;
        if (Selected >= 0 && Selected < _document.Marks.Count)
        {
            _document.Replace(Selected, _document.Marks[Selected] with { Color = color });
            Refresh();
        }
    }

    /// <summary>A drag with the arrow, box or ellipse tool.</summary>
    public void Drag(double x1, double y1, double x2, double y2)
    {
        var index = _document.Add(new Annotation(Tool, x1, y1, x2, y2, Color));
        if (index >= 0)
            Selected = index;
        Refresh();
    }

    /// <summary>A click: places text or a callout, or picks the mark under it with the other tools.</summary>
    public void Click(double x, double y)
    {
        if (Tool is AnnotationKind.Text or AnnotationKind.Callout)
        {
            Selected = _document.Add(new Annotation(Tool, x, y, x, y, Color, Tool == AnnotationKind.Text ? newText : ""));
            Text = Tool == AnnotationKind.Text ? newText : "";
        }
        else
        {
            Selected = _document.At(x, y, Math.Max(1, _width), Math.Max(1, _height));
            Text = SelectedIsText ? _document.Marks[Selected].Text : "";
        }
        Refresh();
    }

    partial void OnTextChanged(string value)
    {
        if (!SelectedIsText || _document.Marks[Selected].Text == value)
            return;
        _document.Replace(Selected, _document.Marks[Selected] with { Text = value });
        Refresh();
    }

    private bool HasSelection() => Selected >= 0;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        _document.Remove(Selected);
        Selected = -1;
        Refresh();
    }

    [RelayCommand]
    private void UndoLast() => Undo();

    [RelayCommand]
    private void RedoLast() => Redo();

    public override bool Undo() => Step(_document.Undo());

    public override bool Redo() => Step(_document.Redo());

    public override bool Delete()
    {
        if (!HasSelection())
            return false;
        Remove();
        return true;
    }

    private bool Step(bool done)
    {
        if (!done)
            return false;
        Selected = -1;
        Refresh();
        return true;
    }

    public override void ApplyTo(Preset preset) => preset.Image.Edit = new ImageEdit { Annotations = [.. Marks] };

    private void Refresh()
    {
        Marks = [.. _document.Marks.Select((mark, i) => (mark, _document.NumberOf(i)))];
        OnPropertyChanged(nameof(SelectedIsText));
        ChangesChanged();
    }

    public override void Dispose()
    {
        Preview?.Dispose();
        Preview = null;
        base.Dispose();
    }
}
