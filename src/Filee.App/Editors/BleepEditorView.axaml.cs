// fork: see BleepEditorView.axaml. The waveform's drags and clicks go to the model.

using Avalonia.Controls;

namespace Filee.App.Editors;

public partial class BleepEditorView : UserControl
{
    public BleepEditorView()
    {
        InitializeComponent();
        Timeline.Marked += (from, to) => Model?.Mark(from, to);
        Timeline.Clicked += seconds => Model?.Pick(seconds);
    }

    private BleepEditorViewModel? Model => DataContext as BleepEditorViewModel;
}
