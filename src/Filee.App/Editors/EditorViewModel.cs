// fork: what every editor window (Phase 5) has in common: the file it opened on, a busy state while its preview loads,
// and Apply or Cancel. Apply never writes a file: the tool's choices go on the operation's preset (ApplyTo) and the
// job queue writes "name Cropped.png" like any other tool (RadialController.Editor.cs).

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Presets;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

public abstract partial class EditorViewModel(string title, string source, ILogger? log) : ObservableObject, IDisposable
{
    private bool _done;

    /// <summary>The tool's name in the header ("Crop").</summary>
    public string Title { get; } = title;

    /// <summary>The file being edited.</summary>
    public string Source { get; } = source;

    public string FileName => Path.GetFileName(Source);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _isBusy;

    /// <summary>The preview could not be loaded: the window shows this instead and Apply stays off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string? _error;

    public bool HasError => Error is not null;

    protected ILogger? Log { get; } = log;

    /// <summary>Apply was pressed: the preset to run is ready (<see cref="ApplyTo"/>).</summary>
    public event Action? Applied;

    /// <summary>Cancel, Escape or the close button: nothing runs.</summary>
    public event Action? Cancelled;

    /// <summary>Loads the preview. A failure shows as <see cref="Error"/> with <paramref name="failed"/>'s text.</summary>
    public async Task LoadAsync(Func<Exception, string> failed)
    {
        IsBusy = true;
        try
        {
            await LoadPreviewAsync();
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "Could not open {File} in the editor", Source);
            Error = failed(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Puts the edit on <paramref name="preset"/>, a copy of the tool's operation.</summary>
    public abstract void ApplyTo(Preset preset);

    protected abstract Task LoadPreviewAsync();

    /// <summary>← or → (<paramref name="direction"/> −1 or 1), with Shift when <paramref name="large"/>; true when the tool used it.</summary>
    public virtual bool Arrow(int direction, bool large) => false;

    /// <summary>Tab; true when the tool used it.</summary>
    public virtual bool Tab() => false;

    /// <summary>Ctrl+Z; true when the tool took something back.</summary>
    public virtual bool Undo() => false;

    /// <summary>Ctrl+Y or Ctrl+Shift+Z; true when the tool did something again.</summary>
    public virtual bool Redo() => false;

    /// <summary>Delete or Backspace; true when the tool removed something.</summary>
    public virtual bool Delete() => false;

    /// <summary>True while the edit would change something (a crop of the whole picture changes nothing).</summary>
    protected virtual bool HasChanges => true;

    /// <summary>Tells the window to check <see cref="CanApply"/> again.</summary>
    protected void ChangesChanged() => ApplyCommand.NotifyCanExecuteChanged();

    private bool CanApply() => !IsBusy && !HasError && !_done && HasChanges;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        _done = true;
        Applied?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_done)
            return;
        _done = true;
        Cancelled?.Invoke();
    }

    public virtual void Dispose() => GC.SuppressFinalize(this);
}
