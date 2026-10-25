// fork: a Tools wheel operation that asks for one value (ToolParameter: Mono or Stereo, Speed 2×, ...). The drop on its
// slice, or Enter during the drag, makes the wheel go and a small popover grow where the drop was; picking a choice
// starts the job and the drop toast takes the same spot. While the drag still goes on, the hook's arrows, Enter and
// Escape drive the popover; once it ended the popover takes the keyboard itself. Escape or a click elsewhere runs
// nothing.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Presets;
using Filee.Core.Web;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed partial class RadialController
{
    private ValuePopoverWindow? _popover;
    private ValuePopoverViewModel? _popoverModel;

    internal ValuePopoverViewModel? Popover => _popoverModel;

    /// <summary>
    /// Asks for the value slice <paramref name="index"/>'s operation needs before converting, if it needs one.
    /// </summary>
    /// <returns>True when the popover took over (it starts the job or disposes <paramref name="web"/>).</returns>
    private bool AskForValue(int index, Preset preset, IReadOnlyList<string> sources, WebDrop? web)
    {
        if (viewModel.IsClickMode || viewModel.ParameterAt(index) is not { } parameter || _window is null)
            return false;
        CancelPopover();
        var at = _window.LastDragScreenPoint ?? _window.PointToScreen(new Point(_window.Bounds.Width / 2, _window.Bounds.Height / 2));
        var title = index < viewModel.Items.Count ? viewModel.Items[index].Label : preset.Name;
        var dropped = _dropped;
        _converted = true; // the drop that ends a drag answered with Enter converts nothing more

        var model = new ValuePopoverViewModel(title, parameter);
        var popover = new ValuePopoverWindow { DataContext = model };
        (_popover, _popoverModel) = (popover, model);
        model.Picked += choice =>
        {
            ClosePopover(model);
            var applied = parameter.Apply(preset, choice);
            log.LogInformation("Converting {Count} file(s) with preset {Preset} ({Choice})", sources.Count, preset.Id,
                parameter.Choices[choice].Suffix);
            if (conversions.Start(sources, applied, showInToast: false, web) is { } job)
                ShowDropToast(job, viewModel.DropToastForTool(job, applied.Name), at);
        };
        model.Cancelled += () =>
        {
            web?.Dispose();
            ClosePopover(model);
        };

        if (dropped)
            HideWindow();
        else
            viewModel.HideWheel(); // Enter mid-drag: the invisible window stays to take the drop
        popover.ShowAt(at);
        if (popover.TryGetPlatformHandle()?.Handle is { } handle)
            platform.RaiseTopmost(handle);
        if (dropped)
            Dispatcher.UIThread.Post(popover.TakeKeyboard);
        return true;
    }

    /// <summary>The drag ended while the popover was up (it opened on Enter): it takes the keyboard now.</summary>
    private void PopoverAfterDrag()
    {
        EditorAfterDrag(); // RadialController.Editor.cs
        if (_popover is { } popover)
            Dispatcher.UIThread.Post(popover.TakeKeyboard);
    }

    /// <summary>A new gesture, or another popover: the one showing runs nothing.</summary>
    private void CancelPopover() => _popoverModel?.Cancel();

    private void ClosePopover(ValuePopoverViewModel model)
    {
        if (_popoverModel != model)
            return;
        _popover?.FadeOut();
        (_popover, _popoverModel) = (null, null);
    }
}
