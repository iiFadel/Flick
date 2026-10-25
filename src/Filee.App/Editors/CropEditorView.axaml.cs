// fork: see CropEditorView.axaml. Links the crop box (Controls/CropCanvas) and the model both ways; Enter in a size box,
// or leaving it, takes the size typed.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Filee.Core.Editing;

namespace Filee.App.Editors;

public partial class CropEditorView : UserControl
{
    private CropEditorViewModel? _model;

    public CropEditorView()
    {
        InitializeComponent();
        foreach (var aspect in CropEditorViewModel.Aspects)
        {
            var button = new Button { CommandParameter = aspect };
            button.Classes.Add("editor-choice");
            if (aspect is "free" or "original")
                button[!ContentProperty] = button.GetResourceObservable("editor.crop." + aspect).ToBinding();
            else
                button.Content = aspect;
            AspectButtons.Children.Add(button);
        }
        Canvas.CropRectChanged += rect => _model?.CropChanged(new NormalizedRect(rect.X, rect.Y, rect.Width, rect.Height));
        foreach (var box in new[] { WidthBox, HeightBox })
        {
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter)
                    return;
                _model?.SetExactSizeCommand.Execute(null);
                e.Handled = true;
            };
            box.LostFocus += (_, _) => _model?.SetExactSizeCommand.Execute(null);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
        {
            _model.AspectRequested -= OnAspect;
            _model.CropRequested -= OnCrop;
            _model.PropertyChanged -= OnModelChanged;
        }
        _model = DataContext as CropEditorViewModel;
        if (_model is null)
            return;
        _model.AspectRequested += OnAspect;
        _model.CropRequested += OnCrop;
        _model.PropertyChanged += OnModelChanged;
        foreach (var button in AspectButtons.Children.OfType<Button>())
            button.Command = _model.SetAspectCommand;
        OnCrop(_model.Crop);
        ShowSelectedAspect();
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CropEditorViewModel.Aspect))
            ShowSelectedAspect();
    }

    private void ShowSelectedAspect()
    {
        foreach (var button in AspectButtons.Children.OfType<Button>())
            button.Classes.Set("selected", Equals(button.CommandParameter, _model?.Aspect));
    }

    private void OnAspect(double ratio) => Canvas.SetAspectRatio(ratio);

    private void OnCrop(NormalizedRect crop) => Canvas.NormalizedCropRect = new Rect(crop.X, crop.Y, crop.Width, crop.Height);
}
