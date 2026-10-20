// fork: Settings → Triggers, "Drag, then tap a key": on/off, the dragging button, where it works, the drag distance
// and the key of each wheel (recorded; modifiers count on their own).

using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Localization;
using Filee.Core.Settings;

namespace Filee.App.ViewModels.Pages;

/// <summary>One wheel's key: shown, recorded, or put back to the default.</summary>
public sealed partial class WheelKeyViewModel(WheelKind kind, string label, DragWheelOptionsViewModel owner) : ObservableObject
{
    public WheelKind Kind { get; } = kind;
    public string Label { get; } = label;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(KeyText))] private bool _isRecording;

    public string KeyText => IsRecording ? owner.RecordingText : WheelKeyText.Display(owner.EffectiveKey(Kind));

    partial void OnIsRecordingChanged(bool value)
    {
        if (value)
            owner.StartRecording(this);
    }

    /// <summary>Called by the view while recording. Returns true when the key was handled.</summary>
    public bool Record(Key key) => IsRecording && owner.Record(this, key);

    [RelayCommand]
    private void Reset() => owner.SetKey(this, DragWheelSettings.DefaultKey(Kind));

    internal void Refresh() => OnPropertyChanged(nameof(KeyText));
}

public sealed partial class DragWheelOptionsViewModel : ObservableObject
{
    private readonly UserDataStore _store;
    private readonly ILocalizer _loc;
    private readonly Action _save;
    private readonly bool _loading;

    public DragWheelOptionsViewModel(UserDataStore store, ILocalizer loc, Action save)
    {
        _store = store;
        _loc = loc;
        _save = save;
        Buttons = Enum.GetValues<TriggerMouseButton>().Where(b => b != TriggerMouseButton.None)
            .Select(b => new Choice<TriggerMouseButton>(b, loc[$"triggers.button.{b}"])).ToList();
        var platform = OperatingSystem.IsMacOS() ? "_macos" : OperatingSystem.IsLinux() ? "_linux" : "";
        Scopes = Enum.GetValues<TriggerScope>()
            .Select(s => new Choice<TriggerScope>(s, loc[s == TriggerScope.FileManager ? $"triggers.scope.{s}{platform}" : $"triggers.scope.{s}"]))
            .ToList();
        Keys = Enum.GetValues<WheelKind>().Select(k => new WheelKeyViewModel(k, loc[$"triggers.wheel.{k}"], this)).ToList();

        _loading = true;
        Enabled = Settings.Enabled;
        Button = Buttons.FirstOrDefault(b => b.Value == Settings.Button) ?? Buttons[0];
        Scope = Scopes.First(s => s.Value == Settings.Scope);
        DragThreshold = Settings.DragThreshold;
        _loading = false;
    }

    private DragWheelSettings Settings => _store.Settings.DragWheel;

    public IReadOnlyList<Choice<TriggerMouseButton>> Buttons { get; }
    public IReadOnlyList<Choice<TriggerScope>> Scopes { get; }
    public IReadOnlyList<WheelKeyViewModel> Keys { get; }

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private Choice<TriggerMouseButton> _button = null!;
    [ObservableProperty] private Choice<TriggerScope> _scope = null!;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ThresholdText))] private int _dragThreshold;

    /// <summary>Why the last key pressed while recording wasn't taken, or null.</summary>
    [ObservableProperty] private string? _error;

    public string ThresholdText => DragThreshold == 0 ? _loc["triggers.threshold_system"] : $"{DragThreshold}px";

    internal string RecordingText => _loc["triggers.recording_key"];

    internal string EffectiveKey(WheelKind kind) => Settings.EffectiveKeys().First(k => k.Value == kind).Key;

    partial void OnButtonChanged(Choice<TriggerMouseButton>? oldValue, Choice<TriggerMouseButton> newValue) => Choices.Restore(oldValue, newValue, v => Button = v);
    partial void OnScopeChanged(Choice<TriggerScope>? oldValue, Choice<TriggerScope> newValue) => Choices.Restore(oldValue, newValue, v => Scope = v);

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is not (nameof(Enabled) or nameof(Button) or nameof(Scope) or nameof(DragThreshold)) ||
            Button is null || Scope is null)
            return;
        Settings.Enabled = Enabled;
        Settings.Button = Button.Value;
        Settings.Scope = Scope.Value;
        Settings.DragThreshold = Math.Clamp(DragThreshold, 0, 200);
        _save();
    }

    /// <summary>One recorder at a time.</summary>
    internal void StartRecording(WheelKeyViewModel recorder)
    {
        Error = null;
        foreach (var other in Keys.Where(k => k != recorder))
            other.IsRecording = false;
    }

    /// <summary>
    /// The key pressed becomes the wheel's key. Keys the open wheel uses (arrows, Enter, Space, Esc, Backspace) and the
    /// key of another wheel are refused with the reason. Returns true when the key was handled.
    /// </summary>
    internal bool Record(WheelKeyViewModel recorder, Key key)
    {
        recorder.IsRecording = false;
        if (WheelKeyText.FromKey(key) is not { } name)
        {
            Error = _loc["triggers.flip_key_unknown"];
            return true;
        }
        var takenByOther = Keys.Any(k => k != recorder && string.Equals(EffectiveKey(k.Kind), name, StringComparison.OrdinalIgnoreCase));
        if (!DragWheelSettings.IsKeyAllowed(name) || takenByOther)
        {
            Error = _loc.Format("triggers.wheel_key_taken", WheelKeyText.Display(name));
            return true;
        }
        SetKey(recorder, name);
        return true;
    }

    internal void SetKey(WheelKeyViewModel recorder, string name)
    {
        recorder.IsRecording = false;
        Error = null;
        // The other wheels keep the keys they have now, even when those were defaults standing in for broken ones.
        var keys = Keys.ToDictionary(k => k.Kind, k => EffectiveKey(k.Kind));
        keys[recorder.Kind] = name;
        if (keys.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() < keys.Count)
        {
            Error = _loc.Format("triggers.wheel_key_taken", WheelKeyText.Display(name));
            return;
        }
        (Settings.ConvertKey, Settings.ToolsKey, Settings.ActionsKey) = (keys[WheelKind.Convert], keys[WheelKind.Tools], keys[WheelKind.Actions]);
        _save();
        foreach (var k in Keys)
            k.Refresh();
    }
}
