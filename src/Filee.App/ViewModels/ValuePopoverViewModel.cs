// fork: the small popover a Tools wheel operation shows after the drop to ask for its one value (ValuePopoverWindow):
// the tool's name and a row of choices, one selected. Arrows move the selection, Enter or a click picks, Escape
// cancels (ToolParameter holds the choices).

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Wheels;

namespace Filee.App.ViewModels;

public sealed partial class ValuePopoverViewModel : ObservableObject
{
    private readonly ToolParameter _parameter;
    private bool _done;

    public ValuePopoverViewModel(string title, ToolParameter parameter)
    {
        Title = title;
        _parameter = parameter;
        Choices = new ObservableCollection<ValueChoice>(parameter.Choices.Select((c, i) => new ValueChoice(this, i, c.Label)));
        Select(parameter.Default);
    }

    public string Title { get; }

    public ObservableCollection<ValueChoice> Choices { get; }

    /// <summary>The choice Enter would pick.</summary>
    public int Selected { get; private set; }

    /// <summary>A choice was picked (once): its index.</summary>
    public event Action<int>? Picked;

    /// <summary>Escape, or the popover lost the user's attention: nothing runs (once).</summary>
    public event Action? Cancelled;

    public void Move(int step) => Select(_parameter.Move(Selected, step));

    public void Apply() => Pick(Selected);

    public void Cancel()
    {
        if (_done)
            return;
        _done = true;
        Cancelled?.Invoke();
    }

    [RelayCommand]
    private void Pick(int index)
    {
        if (_done || index < 0 || index >= Choices.Count)
            return;
        _done = true;
        Select(index);
        Picked?.Invoke(index);
    }

    private void Select(int index)
    {
        Selected = Math.Clamp(index, 0, Math.Max(0, Choices.Count - 1));
        foreach (var choice in Choices)
            choice.IsSelected = choice.Index == Selected;
    }
}

/// <summary>One choice of the popover.</summary>
public sealed partial class ValueChoice(ValuePopoverViewModel owner, int index, string label) : ObservableObject
{
    public ValuePopoverViewModel Owner { get; } = owner;

    public int Index { get; } = index;

    public string Label { get; } = label;

    [ObservableProperty] private bool _isSelected;
}
