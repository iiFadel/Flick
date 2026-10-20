using Filee.App.Services.Triggers;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class DragWheelDetectorTests
{
    private const TriggerMouseButton Left = TriggerMouseButton.Left;
    private const TriggerMouseButton None = TriggerMouseButton.None;

    /// <summary>Records every event in order, as text: "open Convert 50,0", "switch Tools", "nav +1", ...</summary>
    private sealed class Recorder
    {
        public List<string> Events { get; } = [];

        public Recorder(DragWheelDetector detector)
        {
            detector.WheelOpened += (kind, x, y) => Events.Add($"open {kind} {x},{y}");
            detector.WheelSwitched += kind => Events.Add($"switch {kind}");
            detector.Navigate += step => Events.Add($"nav {step:+0;-0}");
            detector.Apply += () => Events.Add("apply");
            detector.Cancel += () => Events.Add("cancel");
            detector.DragEnded += (x, y) => Events.Add($"end {x},{y}");
        }
    }

    private static (DragWheelDetector Detector, Recorder Recorder) Create(DragWheelSettings? settings = null)
    {
        var detector = new DragWheelDetector { SystemDragThreshold = 4 };
        detector.Configure(settings ?? new DragWheelSettings { ConvertKey = "Shift", ToolsKey = "Ctrl", ActionsKey = "Alt" });
        return (detector, new Recorder(detector));
    }

    /// <summary>Left button down at 0,0 and dragged to 50,0: past the threshold, no wheel yet.</summary>
    private static void StartDrag(DragWheelDetector detector)
    {
        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseMove(30, 0, Left);
        detector.MouseMove(50, 0, Left);
    }

    private static void Tap(DragWheelDetector detector, string key)
    {
        detector.KeyDown(key);
        detector.KeyUp(key);
    }

    [Theory]
    [InlineData("LeftShift", WheelKind.Convert)]
    [InlineData("LeftControl", WheelKind.Tools)]
    [InlineData("LeftAlt", WheelKind.Actions)]
    public void Tap_after_the_drag_started_opens_the_keys_wheel_at_the_cursor(string key, WheelKind kind)
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, key);

        Assert.Equal([$"open {kind} 50,0"], recorder.Events);
    }

    [Fact]
    public void Right_hand_modifiers_open_the_same_wheel()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "RightControl");

        Assert.Equal(["open Tools 50,0"], recorder.Events);
    }

    [Fact]
    public void Releasing_the_key_keeps_the_wheel_open()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        detector.KeyDown("LeftShift");
        detector.KeyUp("LeftShift");
        detector.MouseMove(60, 10, Left);
        Tap(detector, "Right");

        Assert.Equal(["open Convert 50,0", "nav +1"], recorder.Events);
    }

    [Fact]
    public void Tap_before_the_threshold_opens_the_wheel_once_the_drag_starts()
    {
        var (detector, recorder) = Create();

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseMove(2, 1, Left);
        Tap(detector, "LeftShift");
        Assert.Empty(recorder.Events);
        detector.MouseMove(20, 0, Left);
        detector.MouseMove(40, 0, Left);

        Assert.Equal(["open Convert 20,0"], recorder.Events);
    }

    [Fact]
    public void Tap_before_the_threshold_then_release_opens_nothing()
    {
        var (detector, recorder) = Create();

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        Tap(detector, "LeftShift");
        detector.MouseUp(Left, 0, 0);
        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseMove(50, 0, Left);

        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void Of_two_taps_before_the_threshold_the_last_wins()
    {
        var (detector, recorder) = Create();

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        Tap(detector, "LeftShift");
        Tap(detector, "LeftAlt");
        detector.MouseMove(50, 0, Left);

        Assert.Equal(["open Actions 50,0"], recorder.Events);
    }

    [Fact]
    public void Key_held_since_before_the_press_opens_nothing_until_tapped_again()
    {
        var (detector, recorder) = Create();

        detector.KeyDown("LeftShift"); // Shift-click selecting files, then dragging them
        detector.MouseDown(Left, 0, 0, ModifierKeys.Shift);
        detector.MouseMove(50, 0, Left);
        detector.KeyDown("LeftShift"); // auto-repeat
        Assert.Empty(recorder.Events);

        detector.KeyUp("LeftShift");
        detector.KeyDown("LeftShift");

        Assert.Equal(["open Convert 50,0"], recorder.Events);
    }

    [Fact]
    public void A_key_up_the_hook_missed_is_caught_up_at_the_next_press()
    {
        var (detector, recorder) = Create();

        detector.KeyDown("LeftShift"); // its key-up never arrives (secure desktop, hook restarted)
        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseMove(50, 0, Left);
        detector.KeyDown("LeftShift");

        Assert.Equal(["open Convert 50,0"], recorder.Events);
    }

    [Fact]
    public void Auto_repeat_neither_reopens_nor_switches()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        detector.KeyDown("LeftShift");
        detector.KeyDown("LeftShift");
        detector.KeyDown("LeftShift");
        detector.KeyDown("LeftControl");
        detector.KeyDown("LeftControl");

        Assert.Equal(["open Convert 50,0", "switch Tools"], recorder.Events);
    }

    [Fact]
    public void Double_tap_of_the_open_wheels_key_does_nothing_more()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "LeftShift");

        Assert.Equal(["open Convert 50,0"], recorder.Events);
    }

    [Fact]
    public void Another_wheels_key_switches_mid_drag()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "LeftControl");
        Tap(detector, "LeftAlt");
        Tap(detector, "RightShift");

        Assert.Equal(["open Convert 50,0", "switch Tools", "switch Actions", "switch Convert"], recorder.Events);
    }

    [Fact]
    public void Arrows_move_the_selection_only_while_a_wheel_is_open()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "Right");
        Tap(detector, "LeftShift");
        Tap(detector, "Right");
        Tap(detector, "Down");
        Tap(detector, "Left");
        Tap(detector, "Up");

        Assert.Equal(["open Convert 50,0", "nav +1", "nav +1", "nav -1", "nav -1"], recorder.Events);
    }

    [Fact]
    public void Enter_is_reported_each_time_and_the_release_then_ends_the_drag()
    {
        var (detector, recorder) = Create();

        // A group's slice: the first Enter opens its ring, the arrows move on it, the second Enter converts.
        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "Enter");
        Tap(detector, "Right");
        Tap(detector, "Enter");
        detector.MouseUp(Left, 70, 5);

        Assert.Equal(["open Convert 50,0", "apply", "nav +1", "apply", "end 70,5"], recorder.Events);
    }

    [Fact]
    public void Numpad_enter_applies_too()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "NumPadEnter");

        Assert.Equal(["open Convert 50,0", "apply"], recorder.Events);
    }

    [Fact]
    public void Escape_cancels_and_nothing_reopens_until_the_button_is_released()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "Escape");
        Tap(detector, "LeftShift");
        Tap(detector, "Enter");
        detector.MouseUp(Left, 50, 0);
        Assert.Equal(["open Convert 50,0", "cancel"], recorder.Events);

        StartDrag(detector);
        Tap(detector, "LeftAlt");

        Assert.Equal(["open Convert 50,0", "cancel", "open Actions 50,0"], recorder.Events);
    }

    [Fact]
    public void Escape_mid_switch_cancels_and_never_applies()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.KeyDown("LeftControl");
        Tap(detector, "Escape");
        detector.KeyUp("LeftControl");
        detector.MouseUp(Left, 50, 0);

        Assert.Equal(["open Convert 50,0", "switch Tools", "cancel"], recorder.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Escape_before_a_wheel_opened_stops_this_drag_from_opening_one(bool pastThreshold)
    {
        var (detector, recorder) = Create();

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        if (pastThreshold)
            detector.MouseMove(50, 0, Left);
        Tap(detector, "Escape");
        Tap(detector, "LeftShift");
        detector.MouseMove(80, 0, Left);
        detector.MouseUp(Left, 80, 0);

        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void Key_held_through_the_drop_ends_the_drag_and_opens_nothing_on_the_next_one()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        detector.KeyDown("LeftShift");
        detector.MouseUp(Left, 50, 0);
        detector.MouseDown(Left, 0, 0, ModifierKeys.Shift); // Shift still down
        detector.MouseMove(50, 0, Left);
        detector.KeyDown("LeftShift"); // auto-repeat

        Assert.Equal(["open Convert 50,0", "end 50,0"], recorder.Events);
    }

    [Fact]
    public void Release_outside_the_wheel_reports_where()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.MouseMove(900, 400, Left);
        detector.MouseUp(Left, 900, 400);

        Assert.Equal(["open Convert 50,0", "end 900,400"], recorder.Events);
    }

    [Fact]
    public void A_plain_drag_reports_nothing()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "A");
        Tap(detector, "Right");
        Tap(detector, "Enter");
        detector.MouseUp(Left, 50, 0);

        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void A_click_then_a_tap_opens_nothing()
    {
        var (detector, recorder) = Create();

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseUp(Left, 0, 0);
        Tap(detector, "LeftShift");
        detector.MouseMove(50, 0, None);
        Tap(detector, "LeftShift");

        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void Only_the_configured_button_drags()
    {
        var (detector, recorder) = Create(new DragWheelSettings { Button = TriggerMouseButton.Right });

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.MouseUp(Left, 50, 0);
        Assert.Empty(recorder.Events);

        detector.MouseDown(TriggerMouseButton.Right, 0, 0, ModifierKeys.None);
        detector.MouseMove(50, 0, TriggerMouseButton.Right);
        Tap(detector, "LeftShift");
        detector.MouseUp(Left, 50, 0); // not the dragging button
        detector.MouseUp(TriggerMouseButton.Right, 60, 0);

        Assert.Equal(["open Convert 50,0", "end 60,0"], recorder.Events);
    }

    [Fact]
    public void Scope_is_checked_where_the_drag_starts()
    {
        var (detector, recorder) = Create();
        detector.ScopeFilter = (x, _) => x < 1000;

        detector.MouseDown(Left, 2000, 0, ModifierKeys.None);
        detector.MouseMove(500, 0, Left); // into the scope while dragging
        Tap(detector, "LeftShift");
        detector.MouseUp(Left, 500, 0);
        Assert.Empty(recorder.Events);

        StartDrag(detector);
        detector.MouseMove(3000, 0, Left); // out of it after starting inside
        Tap(detector, "LeftShift");

        Assert.Equal(["open Convert 3000,0"], recorder.Events);
    }

    [Fact]
    public void Paused_or_switched_off_detector_opens_nothing()
    {
        var (paused, pausedEvents) = Create();
        paused.Paused = true;
        StartDrag(paused);
        Tap(paused, "LeftShift");

        var (off, offEvents) = Create(new DragWheelSettings { Enabled = false });
        StartDrag(off);
        Tap(off, "LeftShift");

        Assert.Empty(pausedEvents.Events);
        Assert.Empty(offEvents.Events);
    }

    [Fact]
    public void Pausing_while_a_wheel_is_open_cancels_it()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.Paused = true;
        Tap(detector, "Enter");
        detector.MouseUp(Left, 50, 0);

        Assert.Equal(["open Convert 50,0", "cancel"], recorder.Events);
    }

    [Fact]
    public void New_settings_while_a_wheel_is_open_cancel_it_and_the_next_drag_uses_them()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.Configure(new DragWheelSettings { ConvertKey = "Tab", ToolsKey = "Ctrl", ActionsKey = "Alt" });
        Tap(detector, "Right");
        detector.MouseUp(Left, 50, 0);
        StartDrag(detector);
        Tap(detector, "LeftShift");
        Tap(detector, "Tab");

        Assert.Equal(["open Convert 50,0", "cancel", "open Convert 50,0"], recorder.Events);
    }

    [Fact]
    public void The_same_settings_again_or_pausing_twice_keep_the_wheel_open()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        // A finished conversion saves the settings file, which applies the settings again.
        detector.Configure(new DragWheelSettings { ConvertKey = "Shift", ToolsKey = "Ctrl", ActionsKey = "Alt" });
        detector.Paused = false;
        Tap(detector, "Right");

        Assert.Equal(["open Convert 50,0", "nav +1"], recorder.Events);
    }

    [Fact]
    public void A_release_the_hook_missed_ends_the_drag_at_the_next_move()
    {
        var (detector, recorder) = Create();

        StartDrag(detector);
        Tap(detector, "LeftShift");
        detector.MouseMove(80, 0, None);
        Tap(detector, "Right");

        Assert.Equal(["open Convert 50,0", "end 80,0"], recorder.Events);
    }

    [Theory]
    [InlineData(0, 4, false)] // system threshold: 4 is not past it
    [InlineData(0, 5, true)]
    [InlineData(20, 15, false)]
    [InlineData(20, 21, true)]
    public void Threshold_comes_from_the_settings_or_the_system(int threshold, int distance, bool opens)
    {
        var (detector, recorder) = Create(new DragWheelSettings { DragThreshold = threshold });

        detector.MouseDown(Left, 0, 0, ModifierKeys.None);
        detector.MouseMove(0, distance, Left);
        Tap(detector, "LeftShift");

        Assert.Equal(opens, recorder.Events.Count == 1);
    }
}
