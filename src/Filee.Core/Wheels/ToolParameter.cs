// fork: the one value a Tools wheel operation asks for in its small popover after the drop (Speed 2×, Mono, 3 parts,
// ...). Pure, unit tested (ToolParameterTests): the popover shows the labels, the keys move the selection, and the
// choice is applied to a copy of the operation's preset.

using Filee.Core.Presets;

namespace Filee.Core.Wheels;

/// <summary>One choice of a <see cref="ToolParameter"/>.</summary>
/// <param name="Label">What the popover shows ("2×", "Mono").</param>
/// <param name="Suffix">The words after the file name ("Speed 2x", "Mono").</param>
/// <param name="Apply">Sets the preset's options for this choice.</param>
public sealed record ToolChoice(string Label, string Suffix, Action<Preset> Apply);

/// <summary>The value an operation asks for: its choices, the one selected at first.</summary>
public sealed record ToolParameter(IReadOnlyList<ToolChoice> Choices, int Default = 0)
{
    /// <summary>The choice <paramref name="step"/> places from <paramref name="selected"/>, wrapping around.</summary>
    public int Move(int selected, int step)
    {
        var count = Choices.Count;
        return count == 0 ? -1 : ((selected + step) % count + count) % count;
    }

    /// <summary>A copy of <paramref name="preset"/> with choice <paramref name="choice"/> applied, named after it.</summary>
    public Preset Apply(Preset preset, int choice)
    {
        var picked = Choices[choice];
        var copy = preset.Clone();
        picked.Apply(copy);
        copy.Name = $"{preset.Name} {picked.Label}";
        copy.Output.FileNamePattern = "{name} " + picked.Suffix;
        return copy;
    }
}
