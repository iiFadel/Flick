// fork: see TrimEditorView.axaml. The timeline's drags go to the model; the window passes on the arrow keys.

using Avalonia.Controls;

namespace Filee.App.Editors;

public partial class TrimEditorView : UserControl
{
    public TrimEditorView()
    {
        InitializeComponent();
        Timeline.HandleMoved += (handle, seconds) => (DataContext as TrimEditorViewModel)?.Move(handle, seconds);
    }
}
