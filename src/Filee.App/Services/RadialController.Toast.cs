// fork: Tangerine's drop toast for the drag wheels (docs/TANGERINE-SPEC.md, "After the drop"). Instead of the progress
// ring, the wheel stays lit for a moment and goes, and a small toast grows at the drop point with the job's progress.
// It closes shortly after the job is done; a failure, or a job still running after HandOffAfter, moves to the
// bottom-right toast. After Enter started a job mid-drag the wheel's window stays up, invisible, so the drop that ends
// the drag lands on it (and converts nothing) instead of on the window below.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Conversion;

namespace Filee.App.Services;

public sealed partial class RadialController
{
    private static readonly TimeSpan WheelHold = TimeSpan.FromMilliseconds(50);  // the lit wheel before it goes
    private static readonly TimeSpan ToastDelay = TimeSpan.FromMilliseconds(70); // from the wheel going to the toast
    private static readonly TimeSpan DoneHold = TimeSpan.FromMilliseconds(80);   // "1 of 1 saved" before it fades

    private DropToastWindow? _toast;
    private DropToastViewModel? _toastModel;
    private bool _toastShown;
    private bool _toastDone;
    private IDisposable? _toastHandOff;

    internal DropToastWindow? Toast => _toast;

    /// <summary>Shows the drop toast for <paramref name="job"/>, started from slice <paramref name="index"/> of a drag wheel.</summary>
    private void ShowDropToast(JobViewModel job, int index) => ShowDropToast(job, viewModel.DropToastFor(job, index), null, index);

    /// <summary>
    /// Shows <paramref name="model"/>'s drop toast; at <paramref name="from"/> when the wheel already went (the value
    /// popover was there), else where the drag was, after the lit slice <paramref name="index"/> lingered.
    /// </summary>
    private void ShowDropToast(JobViewModel job, DropToastViewModel model, PixelPoint? from, int index = -1)
    {
        ReleaseDropToast();
        var dropped = _dropped;
        var at = from ?? _window?.LastDragScreenPoint ?? _window?.PointToScreen(new Point(_window.Bounds.Width / 2, _window.Bounds.Height / 2))
                 ?? new PixelPoint(0, 0);
        (_toastModel, _toastShown, _toastDone) = (model, false, false);
        model.Closing += () => CloseDropToast(model);
        job.PropertyChanged += OnToastJobChanged;
        _toastHandOff = DispatcherTimer.RunOnce(() =>
        {
            if (_toastModel == model && job.IsRunning)
                HandOffDropToast();
        }, HandOffAfter);

        void Show()
        {
            if (_toastModel != model)
                return;
            var toast = _toast ??= new DropToastWindow();
            toast.DataContext = model;
            toast.ShowAt(at);
            if (toast.TryGetPlatformHandle()?.Handle is { } handle)
                platform.RaiseTopmost(handle);
            _toastShown = true;
            if (_toastDone)
                After(DoneHold, () => CloseDropToast(model));
        }

        if (from is not null)
        {
            After(ToastDelay, Show);
            return;
        }
        // The drop cleared the highlight: the slice stays lit while the wheel lingers.
        if (dropped)
            _window?.DonutControl.SetExternalHighlight(index);
        After(WheelHold, () =>
        {
            if (_toastModel != model)
                return; // a new gesture took the window meanwhile
            if (dropped)
                HideWindow();
            else
                viewModel.HideWheel(); // Enter mid-drag: the window stays to take the drop
            After(ToastDelay, Show);
        });
    }

    private void OnToastJobChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not JobViewModel job || _toastModel is not { } model || model.Job != job ||
            e.PropertyName != nameof(JobViewModel.IsRunning) || job.IsRunning)
            return;
        job.PropertyChanged -= OnToastJobChanged;
        _toastHandOff?.Dispose();
        switch (job.Job.State)
        {
            case JobState.Failed or JobState.CompletedWithErrors:
                conversions.ShowInToast(job); // the bottom-right toast explains what went wrong
                CloseDropToast(model);
                break;
            case JobState.Cancelled:
                CloseDropToast(model);
                break;
            default:
                // "1 of 1 saved" for a moment; a job that finished before the toast showed waits for it.
                _toastDone = true;
                if (_toastShown)
                    After(DoneHold, () => CloseDropToast(model));
                break;
        }
    }

    /// <summary>Still running after <see cref="HandOffAfter"/>: the bottom-right toast follows it from here.</summary>
    private void HandOffDropToast()
    {
        if (_toastModel is not { } model)
            return;
        conversions.ShowInToast(model.Job);
        CloseDropToast(model);
    }

    /// <summary>A new gesture: a running job moves to the bottom-right toast, the drop toast goes.</summary>
    private void ReleaseDropToast()
    {
        if (_toastModel is not { } model)
            return;
        if (model.Job.IsRunning)
            conversions.ShowInToast(model.Job);
        CloseDropToast(model);
    }

    private void CloseDropToast(DropToastViewModel model)
    {
        if (_toastModel != model)
            return;
        model.Job.PropertyChanged -= OnToastJobChanged;
        _toastHandOff?.Dispose();
        _toastHandOff = null;
        _toastModel = null;
        model.Dispose();
        if (_toast is { } toast)
            toast.FadeOut(() => { });
    }

    private static void After(TimeSpan delay, Action action)
    {
        if (!Motion.Enabled || delay <= TimeSpan.Zero)
            action();
        else
            DispatcherTimer.RunOnce(action, delay);
    }
}
