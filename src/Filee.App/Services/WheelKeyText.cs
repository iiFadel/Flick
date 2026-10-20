// fork: wheel keys as people read them, and Avalonia keys as wheel keys (DragWheelSettings).

using Avalonia.Input;
using Filee.Core.Settings;

namespace Filee.App.Services;

public static class WheelKeyText
{
    /// <summary>"Shift", "Ctrl" / "⌃ Control", "Alt" / "⌥ Option", "Win" / "⌘ Command", else the key's own name.</summary>
    public static string Display(string key) => WheelKeys.Normalize(key) switch
    {
        "Ctrl" => OperatingSystem.IsMacOS() ? "⌃ Control" : "Ctrl",
        "Alt" => OperatingSystem.IsMacOS() ? "⌥ Option" : "Alt",
        "Meta" => OperatingSystem.IsMacOS() ? "⌘ Command" : "Win",
        var name => KeyNames.Display(name),
    };

    /// <summary>The wheel keys in wheel order, as text: "Shift, Ctrl, Alt".</summary>
    public static string All(DragWheelSettings settings)
    {
        var keys = settings.EffectiveKeys();
        return string.Join(", ", Enum.GetValues<WheelKind>().Select(kind => Display(keys.First(k => k.Value == kind).Key)));
    }

    /// <summary>A key pressed in Settings as a wheel key name; modifiers count on their own. Null for keys the hook doesn't name.</summary>
    public static string? FromKey(Key key) => key switch
    {
        Key.LeftShift or Key.RightShift => "Shift",
        Key.LeftCtrl or Key.RightCtrl => "Ctrl",
        Key.LeftAlt or Key.RightAlt => "Alt",
        Key.LWin or Key.RWin => "Meta",
        _ => KeyNames.ToHookName(key),
    };
}
