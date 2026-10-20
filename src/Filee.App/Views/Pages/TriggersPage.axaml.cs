using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Filee.App.ViewModels.Pages;

namespace Filee.App.Views.Pages;

public partial class TriggersPage : UserControl
{
    public TriggersPage() => InitializeComponent();

    /// <summary>While "Record" is on, the next key combination pressed on the button becomes the shortcut.</summary>
    private void OnRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is ToggleButton { DataContext: TriggerGestureViewModel vm } && vm.IsRecording)
            e.Handled = vm.Record(e.Key, e.KeyModifiers);
    }

    /// <summary>fork: while "Record" is on, the next key pressed on the button opens that wheel from now on.</summary>
    private void OnWheelRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is ToggleButton { DataContext: WheelKeyViewModel vm } && vm.IsRecording)
            e.Handled = vm.Record(e.Key);
    }

    /// <summary>While "Record" is on, the next key pressed on the button flips the donut from now on.</summary>
    private void OnFlipRecorderKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is ToggleButton { DataContext: FlipOptionsViewModel vm } && vm.IsRecording)
            e.Handled = vm.Record(e.Key);
    }
}
