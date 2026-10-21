// fork: which slices each drag wheel shows. Until the Tools wheel gets its own contents the profile's back face stands
// in for it. Flick has no Actions wheel. The wheels replace flipping during a drag: no flip key, no hint.

using Filee.Core.Settings;

namespace Filee.App.ViewModels;

public sealed partial class RadialViewModel
{
    /// <summary>Shows the slices of <paramref name="kind"/> for the files loaded (<see cref="Load"/>).</summary>
    public void ShowWheel(WheelKind kind)
    {
        _loads++; // the back face is checked here when needed, not after the front is drawn
        FlipKey = null;
        FlipWithWheel = false;
        ShowFace(back: kind == WheelKind.Tools, checkBack: kind == WheelKind.Tools);
        CanFlip = false;
        FlipHint = null;
        FlipHintWheel = false;
        // An empty Convert wheel shows a card instead of the ring (DonutMenu.Look.cs).
        CenterSubtitle = Items.Count == 0 && kind == WheelKind.Convert ? loc["donut.wheel_empty_convert"] : null;
    }
}
