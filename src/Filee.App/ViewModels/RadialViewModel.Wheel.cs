// fork: what each drag wheel shows: docs/TANGERINE-SPEC.md's lists (WheelCatalog), not the toolbar profiles, which keep
// serving the click-mode donut. Convert holds format targets only, Tools only the category's tools; tools Flick can't
// run yet are greyed out as "coming soon" in their place. Files without tools get no Tools wheel. No flipping during a
// drag.

using Filee.App.Controls;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Filee.Core.Wheels;

namespace Filee.App.ViewModels;

public sealed partial class RadialViewModel
{
    /// <summary>Tangerine's tools that a built-in preset already does (tool id → preset id). Compress: PDFs only.</summary>
    private static readonly Dictionary<string, string> BuiltTools = new(StringComparer.Ordinal)
    {
        ["cropImage"] = "tool-crop",
        ["splitPDF"] = "pdf-split",
        ["mergePDF"] = "merge-pdf",
        ["createPDF"] = "merge-pdf",
        ["extractArchive"] = "extract",
    };

    /// <summary>
    /// Shows the slices of <paramref name="kind"/> for the files loaded (<see cref="Load"/>). Returns false, changing
    /// nothing, for a Tools wheel the files have no tools in: the wheel showing stays.
    /// </summary>
    public bool ShowWheel(WheelKind kind)
    {
        var sources = Files.Select(file => FormatRegistry.Detect(file)?.Id ?? "").ToList();
        var face = kind == WheelKind.Tools ? ToolsFace(sources) : ConvertFace(sources);
        if (kind == WheelKind.Tools && face.Items.Count == 0)
            return false;

        _loads++; // the back-face check Load posted must not bring the toolbar profile back
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

    /// <summary>
    /// A drag that opened the Tools wheel with files that have none: nothing shows (the window still takes the drag)
    /// until the Convert key is tapped.
    /// </summary>
    public void HideWheel()
    {
        _loads++;
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
        foreach (var tool in WheelCatalog.Tools(sources))
        {
            var label = loc["wheel.tool." + tool];
            var builtId = tool == "compress" ? sources.All(s => s == "pdf") ? "compress-pdf" : null : BuiltTools.GetValueOrDefault(tool);
            // Not built yet: a stand-in that never runs (its slice is disabled, so PresetAt never returns it).
            var built = builtId is null ? null : store.FindPreset(builtId);
            var preset = built ?? new Preset { Id = "tool:" + tool, Name = label, Kind = PresetKind.Tool };
            presets.Add(preset);
            items.Add(Slice(preset, label, built is null ? loc["donut.tool_coming_soon"] : availability.Check(preset, _formats), tool));
        }
        return new Face(presets, presets.Select(_ => (IReadOnlyList<Preset>)[]).ToList(), items);
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
