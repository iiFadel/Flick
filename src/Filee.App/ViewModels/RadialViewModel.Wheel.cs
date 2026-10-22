// fork: what each drag wheel shows: docs/TANGERINE-SPEC.md's lists (WheelCatalog), not the toolbar profiles, which keep
// serving the click-mode donut. Convert holds format targets only, Tools only the category's tools; tools Flick can't
// run yet are greyed out as "coming soon" in their place. Files without tools get no Tools wheel. No flipping during a
// drag.

using Filee.App.Controls;
using Filee.App.Services;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Filee.Core.Wheels;

namespace Filee.App.ViewModels;

public sealed partial class RadialViewModel
{
    /// <summary>
    /// Tangerine's tools that a built-in preset already does (tool id → preset id). The others Flick runs are
    /// <see cref="ToolOperations"/>.
    /// </summary>
    private static readonly Dictionary<string, string> BuiltTools = new(StringComparer.Ordinal)
    {
        ["cropImage"] = "tool-crop",
        ["splitPDF"] = "pdf-split",
        ["mergePDF"] = "merge-pdf",
        ["createPDF"] = "merge-pdf",
    };

    /// <summary>
    /// Shows the slices of <paramref name="kind"/> for the files loaded (<see cref="Load"/>). Returns false, changing
    /// nothing, for a Tools wheel the files have no tools in: the wheel showing stays.
    /// </summary>
    public bool ShowWheel(WheelKind kind)
    {
        // Folders don't convert (RadialController.SourcesFor): the wheels are for the files beside them.
        var sources = Files.Where(file => !Directory.Exists(file)).Select(file => FormatRegistry.Detect(file)?.Id ?? "").ToList();
        var face = kind == WheelKind.Tools ? ToolsFace(sources) : ConvertFace(sources);
        if (kind == WheelKind.Tools && face.Items.Count == 0)
            return false;

        _loads++; // the back-face check Load posted must not bring the toolbar profile back
        Wheel = kind;
        IsBackFace = false;
        (Presets, _subPresets, Items) = (face.Presets, face.SubPresets, face.Items);
        FlipKey = null;
        FlipWithWheel = false;
        CanFlip = false;
        FlipHint = null;
        FlipHintWheel = false;
        DonutOpacity = 1;
        HitBackground = "Transparent";
        // An empty Convert wheel shows a card instead of the ring (DonutMenu.Look.cs).
        CenterSubtitle = Items.Count == 0 ? loc["donut.wheel_empty_convert"] : null;
        return true;
    }

    /// <summary>The drag wheel showing (<see cref="ShowWheel"/>), or null.</summary>
    public WheelKind? Wheel { get; private set; }

    /// <summary>The Tools wheel's operations that ask for a value after the drop, by slice.</summary>
    private Dictionary<int, ToolParameter> _parameters = [];

    /// <summary>The value slice <paramref name="index"/> of the Tools wheel asks for after the drop, or null.</summary>
    public ToolParameter? ParameterAt(int index) => Wheel == WheelKind.Tools ? _parameters.GetValueOrDefault(index) : null;

    /// <summary>The drop toast for <paramref name="job"/>, started from slice <paramref name="index"/> of the wheel.</summary>
    public DropToastViewModel DropToastFor(JobViewModel job, int index)
    {
        var label = index >= 0 && index < Items.Count ? Items[index].Label : job.Job.PresetDisplayName;
        return new DropToastViewModel(job, DropToastLayout.Title(loc, label, conversion: Wheel != WheelKind.Tools), loc);
    }

    /// <summary>The drop toast for <paramref name="job"/> of a tool, titled <paramref name="label"/> ("Channels Mono").</summary>
    public DropToastViewModel DropToastForTool(JobViewModel job, string label) =>
        new(job, DropToastLayout.Title(loc, label, conversion: false), loc);

    /// <summary>
    /// A drag that opened the Tools wheel with files that have none: nothing shows (the window still takes the drag)
    /// until the Convert key is tapped.
    /// </summary>
    public void HideWheel()
    {
        _loads++;
        Wheel = null;
        (Presets, _subPresets, Items) = (Face.Empty.Presets, Face.Empty.SubPresets, Face.Empty.Items);
        CenterSubtitle = null;
        DonutOpacity = 0;
        HitBackground = "#01000000";
    }

    private Face ConvertFace(IReadOnlyList<string> sources)
    {
        var targets = WheelCatalog.Targets(sources);
        var presets = targets.Select(FormatPreset).ToList();
        var items = presets.Select((preset, i) => Slice(preset, WheelCatalog.Label(targets[i]), availability.Check(preset, _formats)));
        return new Face(presets, presets.Select(_ => (IReadOnlyList<Preset>)[]).ToList(), items.ToList());
    }

    /// <summary>The plain built-in conversion to <paramref name="target"/>, or one made for the wheel when there is none.</summary>
    private Preset FormatPreset(string target)
    {
        var format = WheelCatalog.RegistryId(target);
        return store.FindPreset("to-" + format) is { Kind: PresetKind.Convert } preset && preset.TargetFormat == format
            ? preset
            : new Preset { Id = "wheel-to-" + format, Name = WheelCatalog.Label(target), TargetFormat = format };
    }

    private Face ToolsFace(IReadOnlyList<string> sources)
    {
        var presets = new List<Preset>();
        var items = new List<DonutItem>();
        _parameters = [];
        foreach (var tool in WheelCatalog.Tools(sources))
        {
            var label = loc["wheel.tool." + tool];
            var builtId = BuiltTools.GetValueOrDefault(tool);
            var built = ToolOperations.For(tool, sources, id => loc["tool.suffix." + id]) is { } operation
                ? Named(operation, label)
                : builtId is null ? null : store.FindPreset(builtId);
            if (built?.Id.StartsWith("op-", StringComparison.Ordinal) == true && ToolOperations.ParameterFor(tool, key => loc[key]) is { } parameter)
                _parameters[presets.Count] = parameter;
            // Not built yet: a stand-in that never runs (its slice is disabled, so PresetAt never returns it).
            var preset = built ?? new Preset { Id = "tool:" + tool, Name = label, Kind = PresetKind.Tool };
            presets.Add(preset);
            items.Add(Slice(preset, label, built is null ? loc["donut.tool_coming_soon"] : availability.Check(preset, _formats), tool));
        }
        return new Face(presets, presets.Select(_ => (IReadOnlyList<Preset>)[]).ToList(), items);
    }

    private static Preset Named(Preset preset, string name)
    {
        preset.Name = name;
        return preset;
    }

    private static DonutItem Slice(Preset preset, string label, string? reason, string? tool = null) => new()
    {
        Id = preset.Id,
        Label = label,
        IsEnabled = reason is null,
        DisabledReason = reason,
        Icon = tool is null ? null : WheelIcons.Find(tool),
    };
}
