// fork: a Tools wheel operation that opens an editor window (Phase 5: Crop, ...). The drop on its slice, or Enter
// during the drag, makes the wheel go and the editor open on the file; Apply puts the edit on a copy of the operation
// and starts the job, with the drop toast where the drop was. Cancel runs nothing. Files from a browser are saved
// first. Opened on Enter, the editor takes the keyboard once the drag is over (PopoverAfterDrag).

using System.IO;
using Avalonia;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.Core.Presets;
using Filee.Core.Web;
using Microsoft.Extensions.Logging;

namespace Filee.App.Services;

public sealed partial class RadialController
{
    private EditorWindow? _editor;
    private bool _editorWaitsForDrag; // opened on Enter: takes the keyboard when that drag ends

    internal EditorViewModel? Editor => _editor?.DataContext as EditorViewModel;

    /// <summary>Opens the editor slice <paramref name="index"/>'s operation needs, if it needs one.</summary>
    /// <returns>True when the editor took over (it starts the job or disposes <paramref name="web"/>).</returns>
    private bool OpenEditor(int index, Preset preset, IReadOnlyList<string> sources, WebDrop? web)
    {
        if (viewModel.IsClickMode || viewModel.EditorAt(index) is not { } editor || sources.Count != 1 || _window is null)
            return false;
        CancelPopover();
        var at = _window.LastDragScreenPoint ?? _window.PointToScreen(new Point(_window.Bounds.Width / 2, _window.Bounds.Height / 2));
        var title = index < viewModel.Items.Count ? viewModel.Items[index].Label : preset.Name;
        var dropped = _dropped;
        _converted = true; // the drop that ends a drag answered with Enter converts nothing more
        if (dropped)
            HideWindow();
        else
            viewModel.HideWheel(); // Enter mid-drag: the invisible window stays to take the drop

        if (web is null)
            Open(sources[0]);
        else
            _ = OpenForBrowserFileAsync();
        return true;

        async Task OpenForBrowserFileAsync()
        {
            var files = await conversions.SaveBrowserFilesAsync(web, (placeholder, ex) =>
                log.LogWarning(ex, "Saving {File} from a browser for {Preset} failed", Path.GetFileName(placeholder), preset.Id));
            if (files.Count > 0)
                Open(files[0]);
        }

        void Open(string source)
        {
            _editor?.Close();
            var (window, model) = windows.ShowEditor(editor.Kind, source, title, at, activate: _dropped);
            (_editor, _editorWaitsForDrag) = (window, !_dropped);
            window.Closed += (_, _) =>
            {
                if (_editor == window)
                    _editor = null;
            };
            model.Cancelled += window.Close;
            model.Applied += () =>
            {
                var applied = preset.Clone();
                model.ApplyTo(applied);
                window.Close();
                log.LogInformation("Editing {File} with {Tool}", Path.GetFileName(source), editor.Tool);
                if (conversions.Start([source], applied, showInToast: false) is { } job)
                    ShowDropToast(job, viewModel.DropToastForTool(job, applied.Name), at);
            };
        }
    }

    /// <summary>The drag ended while the editor was up (it opened on Enter): it takes the keyboard now.</summary>
    private void EditorAfterDrag()
    {
        if (_editor is not { } editor || !_editorWaitsForDrag)
            return;
        _editorWaitsForDrag = false;
        Dispatcher.UIThread.Post(editor.TakeKeyboard);
    }
}
