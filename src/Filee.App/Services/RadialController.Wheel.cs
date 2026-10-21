// fork: the drag wheels. A wheel key tapped during a drag (TriggerService.Wheel.cs) opens the invisible drag window at
// the cursor like upstream's drag gesture did; once the files enter it shows that wheel, nothing selected. The other
// wheel's key switches the slices in place, except to a Tools wheel the files have none in: that key does nothing.
// Arrows and Enter from the hook drive the donut's keyboard, Escape closes. Once a conversion started (drop or Enter) the wheel keys are ignored; the drop that ends the drag is too.

using Avalonia.Input;
using Filee.App.Controls;
using Filee.Core.Settings;

namespace Filee.App.Services;

public sealed partial class RadialController
{
    private WheelKind? _wheelKind;

    /// <summary>Subscribes to the wheel events of the trigger service. Called once at start-up.</summary>
    public void ListenToWheels()
    {
        triggers.WheelOpened += (kind, x, y) =>
        {
            _wheelKind = kind;
            ShowForDrag(x, y);
        };
        triggers.WheelSwitched += kind =>
        {
            var before = _wheelKind;
            _wheelKind = kind;
            if (!WheelShowsFiles)
                return;
            // The new wheel replaces the old one in place, without blooming again (Tangerine); one that wasn't showing
            // yet (opened with a key that had nothing to show) blooms now.
            var hidden = viewModel.DonutOpacity == 0;
            if (!ShowWheel(_window!.DonutControl))
                _wheelKind = before;
            else if (hidden)
                _window.DonutControl.PlayOpenAnimation();
        };
        triggers.WheelNavigated += step =>
        {
            if (WheelShowsFiles)
                _window!.DonutControl.HandleDragKey(step > 0 ? Key.Right : Key.Left);
        };
        triggers.WheelApplied += () =>
        {
            if (WheelShowsFiles)
                _window!.DonutControl.HandleDragKey(Key.Enter);
        };
        triggers.WheelCancelled += () =>
        {
            if (_visible && !viewModel.IsClickMode)
                Close();
        };
        triggers.WheelDragEnded += (_, _) => OnDragGestureEnded();
    }

    /// <summary>A drag wheel shows files and nothing was converted from it yet.</summary>
    private bool WheelShowsFiles =>
        _wheelKind is not null && _window is not null && _visible && !_dropped && !_converted && !viewModel.IsClickMode && viewModel.HasFiles;

    /// <summary>
    /// The files just entered (or the wheel switched): show the wheel's slices. As in Tangerine, none is selected until
    /// the drag reaches one or an arrow picks one (the first Right or Down picks the top slice).
    /// </summary>
    /// <returns>False when the wheel has nothing to show (a Tools wheel for files without tools).</returns>
    private bool ShowWheel(DonutMenu donut)
    {
        if (_wheelKind is not { } kind || viewModel.IsClickMode || !viewModel.ShowWheel(kind))
            return false;
        donut.SetExternalHighlight(-1);
        return true;
    }
}
