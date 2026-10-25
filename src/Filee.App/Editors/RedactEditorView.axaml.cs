// fork: see RedactEditorView.axaml. The canvas's drags and clicks go to the model.

using Avalonia.Controls;

namespace Filee.App.Editors;

public partial class RedactEditorView : UserControl
{
    public RedactEditorView()
    {
        InitializeComponent();
        Canvas.Drawn += (from, to) => Model?.Draw(from.X, from.Y, to.X, to.Y);
        Canvas.Clicked += point => Model?.Pick(point.X, point.Y);
    }

    private RedactEditorViewModel? Model => DataContext as RedactEditorViewModel;
}
