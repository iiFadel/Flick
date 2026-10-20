using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class DragWheelSettingsTests
{
    [Theory]
    [InlineData("LeftShift", "Shift")]
    [InlineData("RightShift", "Shift")]
    [InlineData("LeftControl", "Ctrl")]
    [InlineData("RightControl", "Ctrl")]
    [InlineData("LeftAlt", "Alt")]
    [InlineData("RightAlt", "Alt")]
    [InlineData("LeftMeta", "Meta")]
    [InlineData("RightMeta", "Meta")]
    [InlineData("NumPadEnter", "Enter")]
    [InlineData("Tab", "Tab")]
    [InlineData(" F2 ", "F2")]
    public void Left_and_right_modifiers_are_one_key(string hookName, string expected) =>
        Assert.Equal(expected, WheelKeys.Normalize(hookName));

    [Fact]
    public void Default_keys_map_to_the_three_wheels()
    {
        var keys = new DragWheelSettings().EffectiveKeys();

        Assert.Equal(3, keys.Count);
        Assert.Equal(WheelKind.Convert, keys["Shift"]);
        Assert.Equal(WheelKind.Tools, keys[OperatingSystem.IsMacOS() ? "Meta" : "Ctrl"]);
        Assert.Equal(WheelKind.Actions, keys[OperatingSystem.IsMacOS() ? "Ctrl" : "Alt"]);
    }

    [Fact]
    public void Chosen_keys_are_used_normalised()
    {
        var keys = new DragWheelSettings { ConvertKey = "RightShift", ToolsKey = "Tab", ActionsKey = "F2" }.EffectiveKeys();

        Assert.Equal(WheelKind.Convert, keys["Shift"]);
        Assert.Equal(WheelKind.Tools, keys["tab"]);
        Assert.Equal(WheelKind.Actions, keys["F2"]);
    }

    [Theory]
    [InlineData("Shift", "LeftShift", "Alt")] // two wheels share a key
    [InlineData("Enter", "Ctrl", "Alt")]      // Enter applies the selected slice
    [InlineData("Shift", "Up", "Alt")]        // arrows move the selection
    [InlineData("Shift", "Ctrl", "Escape")]   // Escape cancels
    [InlineData("", "Ctrl", "Alt")]
    public void Keys_that_cannot_work_fall_back_to_the_defaults(string convert, string tools, string actions)
    {
        var keys = new DragWheelSettings { ConvertKey = convert, ToolsKey = tools, ActionsKey = actions }.EffectiveKeys();

        Assert.Equal(new DragWheelSettings().EffectiveKeys(), keys);
    }

    [Fact]
    public void Version_30_drag_gesture_moves_to_the_wheel_trigger()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 30,
            Triggers =
            [
                new TriggerGesture { Kind = TriggerKind.Drag, Enabled = false, Button = TriggerMouseButton.Left },
                new TriggerGesture
                {
                    Kind = TriggerKind.Drag, Modifiers = ModifierKeys.Alt, Button = TriggerMouseButton.Right,
                    DragThreshold = 12, Scope = TriggerScope.Anywhere,
                },
                new TriggerGesture { Kind = TriggerKind.Hold, Modifiers = ModifierKeys.Alt, Button = TriggerMouseButton.Middle },
                new TriggerGesture { Kind = TriggerKind.KeyChord, Modifiers = ModifierKeys.Ctrl, Key = "Q" },
            ],
        };

        SettingsMigrations.Apply(settings);

        // The first enabled drag gesture carries over; every drag gesture goes, the others stay.
        Assert.True(settings.DragWheel.Enabled);
        Assert.Equal(TriggerMouseButton.Right, settings.DragWheel.Button);
        Assert.Equal(12, settings.DragWheel.DragThreshold);
        Assert.Equal(TriggerScope.Anywhere, settings.DragWheel.Scope);
        Assert.Equal([TriggerKind.Hold, TriggerKind.KeyChord], settings.Triggers.Select(t => t.Kind));
        Assert.Equal(new DragWheelSettings().EffectiveKeys(), settings.DragWheel.EffectiveKeys());
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void A_switched_off_drag_gesture_leaves_the_wheel_trigger_off()
    {
        var settings = new AppSettings
        {
            SchemaVersion = 30,
            Triggers = [new TriggerGesture { Kind = TriggerKind.Drag, Enabled = false }],
        };

        SettingsMigrations.Apply(settings);

        Assert.False(settings.DragWheel.Enabled);
        Assert.DoesNotContain(settings.Triggers, t => t.Kind == TriggerKind.Drag);
    }

    [Fact]
    public void Fresh_settings_have_the_wheel_trigger_and_no_drag_gesture()
    {
        var settings = new AppSettings();

        Assert.True(settings.DragWheel.Enabled);
        Assert.DoesNotContain(settings.Triggers, t => t.Kind == TriggerKind.Drag);
        Assert.Contains(settings.Triggers, t => t.Kind == TriggerKind.KeyChord);
    }

    [Fact]
    public void Wheel_settings_survive_a_save_and_load()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Settings.DragWheel.ToolsKey = "Tab";
        store.Settings.DragWheel.Scope = TriggerScope.Anywhere;
        store.SaveSettings();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        Assert.Equal("Tab", reloaded.Settings.DragWheel.ToolsKey);
        Assert.Equal(TriggerScope.Anywhere, reloaded.Settings.DragWheel.Scope);
    }
}
