// fork: which slices each drag wheel shows. Until the Tools and Actions wheels get their own contents (PLAN.md,
// phases 3 and 4) the Tools wheel stands in with the profile's back face and the Actions wheel is empty. The wheels
// replace flipping during a drag: no flip key, no hint.

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
        switch (kind)
        {
            case WheelKind.Convert:
                ShowFace(back: false, checkBack: false);
                break;
            case WheelKind.Tools:
                ShowFace(back: true);
                break;
            default:
                IsBackFace = false;
                (Presets, _subPresets, Items) = (Face.Empty.Presets, Face.Empty.SubPresets, Face.Empty.Items);
                break;
        }
        CanFlip = false;
        FlipHint = null;
        FlipHintWheel = false;
        CenterSubtitle = Items.Count > 0 ? null
            : kind == WheelKind.Actions ? loc["donut.wheel_actions_later"]
            : loc["donut.wheel_empty"];
    }
}
