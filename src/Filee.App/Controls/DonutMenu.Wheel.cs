// fork: the drag wheels' keyboard (DragWheelDetector through the global hook). During an OS drag the donut's window
// has no focus and the drag's position arrives as SetExternalHit, which would take the selection straight back from
// the keys. So a key holds the selection until the drag moves KeyboardJitter away from where it rested; it stays while
// the drag is in the hole, and goes once the drag is on a slice (which is then the selection).

using Avalonia;
using Avalonia.Input;

namespace Filee.App.Controls;

public sealed partial class DonutMenu
{
    private bool _dragKeyHold;
    private Point? _dragKeyAnchor;

    /// <summary>An arrow or Enter from the global hook during a drag (see <see cref="HandleKey"/>).</summary>
    public bool HandleDragKey(Key key)
    {
        if (IsEditMode || _progressMode || Count == 0)
            return false;
        TakeSelectionFromDrag();
        return HandleKey(key);
    }

    /// <summary>The selection is the keyboard's (in <c>_hoverIndex</c>) until the drag moves on.</summary>
    private void TakeSelectionFromDrag()
    {
        if (_externalHighlight >= 0)
            _hoverIndex = _externalHighlight;
        _externalHighlight = -1;
        _externalCenterHot = false;
        _dragKeyHold = true;
        _dragKeyAnchor = _lastExternalAt;
    }

    /// <summary>True when a drag resting where it was when a key was pressed must not change the selection.</summary>
    private bool DragKeyHolds(Point? at)
    {
        if (!_dragKeyHold || at is not { } p)
            return false;
        if (_dragKeyAnchor is not { } anchor)
            _dragKeyAnchor = anchor = p;
        if (Distance(p, anchor) < KeyboardJitter)
            return true;
        _dragKeyHold = false;
        return false;
    }

    /// <summary>A drag on a slice selects it itself: the keyboard's selection goes.</summary>
    private void ForgetKeySelection(DonutHit hit)
    {
        if (hit.Kind is DonutHitKind.Segment or DonutHitKind.SubSegment)
            _hoverIndex = -1;
    }
}
