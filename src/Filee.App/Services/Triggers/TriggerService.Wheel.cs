// fork: feeds the global hook into DragWheelDetector (drag, then tap a key to open a wheel) and raises its events on
// the UI thread. The hook only listens: the keys still reach Explorer, which ignores arrows and Enter during a drag and
// cancels its own drag on Escape, as the wheel does.

using Filee.Core.Settings;

namespace Filee.App.Services.Triggers;

public sealed partial class TriggerService
{
    private readonly DragWheelDetector _wheel = new();
    private volatile TriggerGesture _wheelScope = new() { Scope = TriggerScope.FileManager };

    /// <summary>A wheel key was tapped during a drag: show that wheel at the point (physical pixels) and wait for the files.</summary>
    public event Action<WheelKind, int, int>? WheelOpened;

    /// <summary>Another wheel's key was tapped while a wheel was open.</summary>
    public event Action<WheelKind>? WheelSwitched;

    /// <summary>An arrow key while a wheel is open: +1 clockwise, -1 counter-clockwise.</summary>
    public event Action<int>? WheelNavigated;

    /// <summary>Enter while a wheel is open.</summary>
    public event Action? WheelApplied;

    /// <summary>Escape while a wheel is open (or the trigger was paused or changed).</summary>
    public event Action? WheelCancelled;

    /// <summary>The button of a drag that opened a wheel was released.</summary>
    public event Action<int, int>? WheelDragEnded;

    private void InitWheel()
    {
        _wheel.SystemDragThreshold = _platform.SystemDragThreshold;
        _wheel.ScopeFilter = (x, y) => InScope(_wheelScope, x, y);
        _wheel.WheelOpened += (kind, x, y) => Post(() => WheelOpened?.Invoke(kind, x, y));
        _wheel.WheelSwitched += kind => Post(() => WheelSwitched?.Invoke(kind));
        _wheel.Navigate += step => Post(() => WheelNavigated?.Invoke(step));
        _wheel.Apply += () => Post(() => WheelApplied?.Invoke());
        _wheel.Cancel += () => Post(() => WheelCancelled?.Invoke());
        _wheel.DragEnded += (x, y) => Post(() => WheelDragEnded?.Invoke(x, y));
    }

    private void ApplyWheel(AppSettings settings)
    {
        _wheelScope = new TriggerGesture { Scope = settings.DragWheel.Scope };
        _wheel.Configure(settings.DragWheel);
        _wheel.Paused = settings.Paused;
    }
}
