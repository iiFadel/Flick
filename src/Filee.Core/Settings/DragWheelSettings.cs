// fork: Flick's drag-then-tap trigger. Start dragging files, then tap a key: the key picks the wheel that opens
// (Convert or Tools), the wheel stays open after the key is released, and tapping the other wheel's key switches to
// it. Tangerine's third wheel (Actions) is not in Flick. Replaces upstream's "hold the modifier, then drag" (TriggerKind.Drag).

namespace Filee.Core.Settings;

/// <summary>The wheels a drag can open.</summary>
public enum WheelKind
{
    /// <summary>Target formats.</summary>
    Convert,

    /// <summary>Same-format operations (compress, trim, crop, ...).</summary>
    Tools,
}

/// <summary>Key names as the global hook reports them (SharpHook <c>KeyCode</c> without "Vc").</summary>
public static class WheelKeys
{
    /// <summary>Left and right variants of a modifier are one key: "LeftShift" and "RightShift" are "Shift".</summary>
    public static string Normalize(string key) => key.Trim().ToLowerInvariant() switch
    {
        "leftshift" or "rightshift" or "shift" => "Shift",
        "leftcontrol" or "rightcontrol" or "control" or "ctrl" => "Ctrl",
        "leftalt" or "rightalt" or "alt" => "Alt",
        "leftmeta" or "rightmeta" or "meta" => "Meta",
        "numpadenter" => "Enter",
        _ => key.Trim(),
    };
}

/// <summary>How a drag opens a wheel (Settings → Triggers).</summary>
public sealed class DragWheelSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>The button that drags the files.</summary>
    public TriggerMouseButton Button { get; set; } = TriggerMouseButton.Left;

    /// <summary>Distance in logical pixels the mouse must travel before a drag counts. 0 = system default.</summary>
    public int DragThreshold { get; set; }

    public TriggerScope Scope { get; set; } = TriggerScope.FileManager;

    /// <summary>Key names as <see cref="WheelKeys.Normalize"/> returns them ("Shift", "Ctrl", "Alt", "Meta", "Tab", ...).</summary>
    public string ConvertKey { get; set; } = DefaultKey(WheelKind.Convert);

    public string ToolsKey { get; set; } = DefaultKey(WheelKind.Tools);

    /// <summary>Windows and Linux: Shift, Ctrl. macOS: Shift, Command (as in Tangerine).</summary>
    public static string DefaultKey(WheelKind kind) => kind switch
    {
        WheelKind.Convert => "Shift",
        _ => OperatingSystem.IsMacOS() ? "Meta" : "Ctrl",
    };

    /// <summary>The key set for <paramref name="kind"/>, as saved.</summary>
    public string KeyFor(WheelKind kind) => kind switch
    {
        WheelKind.Convert => ConvertKey,
        _ => ToolsKey,
    };

    /// <summary>
    /// Whether <paramref name="key"/> may open a wheel: not a key the open wheel uses (arrows, Enter, Space, Escape,
    /// Backspace: <see cref="DonutSettings.ReservedFlipKeys"/>).
    /// </summary>
    public static bool IsKeyAllowed(string? key) =>
        !string.IsNullOrWhiteSpace(key) && !DonutSettings.ReservedFlipKeys.Contains(WheelKeys.Normalize(key));

    /// <summary>
    /// Key → wheel, normalised. When a key isn't allowed or two wheels share one (a file edited by hand), both
    /// fall back to the defaults, so every wheel keeps a key.
    /// </summary>
    public IReadOnlyDictionary<string, WheelKind> EffectiveKeys()
    {
        var keys = new Dictionary<string, WheelKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in Enum.GetValues<WheelKind>())
        {
            var key = KeyFor(kind);
            if (!IsKeyAllowed(key) || !keys.TryAdd(WheelKeys.Normalize(key), kind))
                return Enum.GetValues<WheelKind>().ToDictionary(DefaultKey, k => k, StringComparer.OrdinalIgnoreCase);
        }
        return keys;
    }

    public DragWheelSettings Clone() => (DragWheelSettings)MemberwiseClone();
}
