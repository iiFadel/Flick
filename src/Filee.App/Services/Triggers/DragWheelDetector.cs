// fork: pure state machine for Flick's drag-then-tap trigger (see DragWheelSettings). Like GestureDetector it knows
// nothing about SharpHook or Avalonia, so every transition is unit tested (DragWheelDetectorTests).
//
//   Idle ──press (button, scope)──► Pressed ──past threshold──► Dragging ──wheel key──► Open(kind)
//                                     │ wheel key                                       │ other wheel key: switch
//                                     ▼                                                 │ arrows: navigate
//                                  Pending(kind) ──past threshold──► Open(kind)         │ Enter: apply
//                                                                                       │ Escape ──► Cancelled
//   Releasing the wheel key keeps the wheel open; releasing the button ends the drag (reported from Open). Enter is
//   reported every time (on a group's slice it opens the ring, the next Enter converts); once a conversion runs the
//   controller ignores the keys. Escape, or a press that doesn't qualify, ignores keys until the release.

using Filee.Core.Settings;

namespace Filee.App.Services.Triggers;

/// <summary>Detects "drag, then tap a wheel's key" in a stream of input events. Thread-safe.</summary>
public sealed class DragWheelDetector
{
    private enum State { Idle, Ignored, Pressed, Pending, Dragging, Open, Cancelled }

    private readonly object _lock = new();
    private readonly HashSet<string> _down = new(StringComparer.OrdinalIgnoreCase);
    private DragWheelSettings _settings = new() { Enabled = false };
    private IReadOnlyDictionary<string, WheelKind> _keys = new Dictionary<string, WheelKind>();
    private State _state;
    private WheelKind _kind;
    private TriggerMouseButton _button;
    private int _pressX, _pressY;
    private int _lastX, _lastY; // a wheel opened by a key opens where the cursor is
    private bool _paused;

    /// <summary>Decides whether a drag may open a wheel, at the point it starts. Called on the hook thread; must be fast.</summary>
    public Func<int, int, bool> ScopeFilter { get; set; } = (_, _) => true;

    /// <summary>Fallback drag threshold (physical pixels) when the settings say 0 = "system default".</summary>
    public int SystemDragThreshold { get; set; } = 4;

    /// <summary>When true nothing is detected (tray "Pause"); pausing closes an open wheel.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            bool cancel;
            lock (_lock)
            {
                cancel = value && !_paused && Abandon();
                _paused = value;
            }
            if (cancel)
                Cancel?.Invoke();
        }
    }

    /// <summary>A wheel key was tapped during a drag: open that wheel at the cursor (physical pixels).</summary>
    public event Action<WheelKind, int, int>? WheelOpened;

    /// <summary>Another wheel's key was tapped while a wheel was open.</summary>
    public event Action<WheelKind>? WheelSwitched;

    /// <summary>An arrow key while a wheel is open: +1 clockwise (Right, Down), -1 counter-clockwise (Left, Up).</summary>
    public event Action<int>? Navigate;

    /// <summary>Enter while a wheel is open: apply the selected slice to the dragged files (or open its group's ring).</summary>
    public event Action? Apply;

    /// <summary>Escape (or pausing / new settings) while a wheel is open: close it.</summary>
    public event Action? Cancel;

    /// <summary>The button of a drag that opened a wheel was released, at this point.</summary>
    public event Action<int, int>? DragEnded;

    /// <summary>
    /// New settings. Every save of the settings file brings them (a finished conversion counts itself), so a wheel only
    /// closes when something here changed.
    /// </summary>
    public void Configure(DragWheelSettings settings)
    {
        bool cancel;
        lock (_lock)
        {
            var keys = settings.EffectiveKeys();
            if (Same(_settings, settings) && keys.Count == _keys.Count && !keys.Except(_keys).Any())
                return;
            _settings = settings.Clone();
            _keys = keys;
            cancel = Abandon();
        }
        if (cancel)
            Cancel?.Invoke();
    }

    private static bool Same(DragWheelSettings a, DragWheelSettings b) =>
        a.Enabled == b.Enabled && a.Button == b.Button && a.DragThreshold == b.DragThreshold && a.Scope == b.Scope;

    /// <summary>
    /// <paramref name="modifiers"/>: the modifiers held at the press, which also catches up on key releases the hook
    /// missed (a modifier not held now is not down).
    /// </summary>
    public void MouseDown(TriggerMouseButton button, int x, int y, ModifierKeys modifiers)
    {
        lock (_lock)
        {
            ForgetReleasedModifiers(modifiers);
            if (_state != State.Idle)
                return;
            _button = button;
            (_pressX, _pressY) = (_lastX, _lastY) = (x, y);
            _state = _settings.Enabled && !_paused && button == _settings.Button && ScopeFilter(x, y)
                ? State.Pressed
                : State.Ignored;
        }
    }

    /// <summary>Pointer moved (physical pixels). <paramref name="heldButton"/> is the button held now, or None.</summary>
    public void MouseMove(int x, int y, TriggerMouseButton heldButton)
    {
        Action? fire = null;
        lock (_lock)
        {
            if (_state == State.Idle)
                return;
            (_lastX, _lastY) = (x, y);
            if (heldButton == TriggerMouseButton.None)
            {
                // The release never reached the hook: end here.
                fire = EndAt(x, y);
            }
            else if (_state is State.Pressed or State.Pending && PastThreshold(x, y))
            {
                if (_state == State.Pending)
                {
                    var kind = _kind;
                    fire = () => WheelOpened?.Invoke(kind, x, y);
                    _state = State.Open;
                }
                else
                {
                    _state = State.Dragging;
                }
            }
        }
        fire?.Invoke();
    }

    public void MouseUp(TriggerMouseButton button, int x, int y)
    {
        Action? fire;
        lock (_lock)
        {
            if (_state == State.Idle || button != _button)
                return;
            fire = EndAt(x, y);
        }
        fire?.Invoke();
    }

    /// <summary>A key went down. <paramref name="key"/> is the hook's name without "Vc" ("LeftShift", "Enter").</summary>
    public void KeyDown(string key)
    {
        Action? fire = null;
        lock (_lock)
        {
            var name = WheelKeys.Normalize(key);
            if (!_down.Add(name))
                return; // auto-repeat of a held key
            var isWheelKey = _keys.TryGetValue(name, out var kind);
            var isEscape = string.Equals(name, "Escape", StringComparison.OrdinalIgnoreCase);
            switch (_state)
            {
                case State.Pressed or State.Pending or State.Dragging when isEscape:
                    _state = State.Cancelled;
                    break;
                case State.Pressed or State.Pending when isWheelKey:
                    (_state, _kind) = (State.Pending, kind);
                    break;
                case State.Dragging when isWheelKey:
                    (_state, _kind) = (State.Open, kind);
                    var (x, y) = (_lastX, _lastY);
                    fire = () => WheelOpened?.Invoke(kind, x, y);
                    break;
                case State.Open:
                    fire = OnKeyWhileOpen(name, isWheelKey, kind, isEscape);
                    break;
            }
        }
        fire?.Invoke();
    }

    public void KeyUp(string key)
    {
        lock (_lock)
            _down.Remove(WheelKeys.Normalize(key));
    }

    /// <summary>Caller holds the lock.</summary>
    private Action? OnKeyWhileOpen(string name, bool isWheelKey, WheelKind kind, bool isEscape)
    {
        if (isWheelKey)
        {
            if (kind == _kind)
                return null; // a second tap of the open wheel's key
            _kind = kind;
            return () => WheelSwitched?.Invoke(kind);
        }
        if (isEscape)
        {
            _state = State.Cancelled;
            return () => Cancel?.Invoke();
        }
        if (name.Equals("Enter", StringComparison.OrdinalIgnoreCase))
            return () => Apply?.Invoke();
        var step = name.ToLowerInvariant() switch
        {
            "right" or "down" => 1,
            "left" or "up" => -1,
            _ => 0,
        };
        return step == 0 ? null : () => Navigate?.Invoke(step);
    }

    /// <summary>Caller holds the lock.</summary>
    private bool PastThreshold(int x, int y)
    {
        var threshold = _settings.DragThreshold > 0 ? _settings.DragThreshold : SystemDragThreshold;
        return Math.Abs(x - _pressX) > threshold || Math.Abs(y - _pressY) > threshold;
    }

    /// <summary>The button went up (or was found up). Caller holds the lock.</summary>
    private Action? EndAt(int x, int y)
    {
        var reported = _state == State.Open;
        _state = State.Idle;
        return reported ? () => DragEnded?.Invoke(x, y) : null;
    }

    /// <summary>
    /// Pause or new settings: the press in progress is ignored until its release. Returns true when a wheel was open,
    /// which must then be cancelled. Caller holds the lock.
    /// </summary>
    private bool Abandon()
    {
        var open = _state == State.Open;
        if (_state != State.Idle)
            _state = State.Ignored;
        return open;
    }

    /// <summary>Caller holds the lock.</summary>
    private void ForgetReleasedModifiers(ModifierKeys held)
    {
        foreach (var (flag, name) in Modifiers)
            if (!held.HasFlag(flag))
                _down.Remove(name);
    }

    private static readonly (ModifierKeys Flag, string Name)[] Modifiers =
    [
        (ModifierKeys.Shift, "Shift"), (ModifierKeys.Ctrl, "Ctrl"), (ModifierKeys.Alt, "Alt"), (ModifierKeys.Meta, "Meta"),
    ];
}
