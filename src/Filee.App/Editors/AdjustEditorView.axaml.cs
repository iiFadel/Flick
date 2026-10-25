// fork: see AdjustEditorView.axaml. Original shows the picture as it was while it is held down.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Filee.App.Editors;

public partial class AdjustEditorView : UserControl
{
    public AdjustEditorView()
    {
        InitializeComponent();
        OriginalButton.AddHandler(PointerPressedEvent, (_, _) => Show(true), RoutingStrategies.Tunnel, handledEventsToo: true);
        OriginalButton.AddHandler(PointerReleasedEvent, (_, _) => Show(false), RoutingStrategies.Tunnel, handledEventsToo: true);
        OriginalButton.AddHandler(PointerCaptureLostEvent, (_, _) => Show(false), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void Show(bool original)
    {
        if (DataContext is AdjustEditorViewModel model)
            model.ShowOriginal = original;
    }
}
