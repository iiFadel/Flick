// fork: the small toast that grows at the drop point while a drag wheel's conversion runs (DropToastWindow): its title,
// one line of status and the progress bar, following the job.

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.App.Services;
using Filee.Core.Conversion;
using Filee.Core.Localization;

namespace Filee.App.ViewModels;

public sealed partial class DropToastViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizer _loc;

    public DropToastViewModel(JobViewModel job, string title, ILocalizer loc)
    {
        Job = job;
        Title = title;
        _loc = loc;
        job.PropertyChanged += OnJobChanged;
        Update();
    }

    public JobViewModel Job { get; }

    public string Title { get; }

    [ObservableProperty] private string _status = "";

    /// <summary>The bar: 0 to 1.</summary>
    [ObservableProperty] private double _fraction;

    [ObservableProperty] private bool _finished;

    /// <summary>× : cancels a running job; on a finished one it only closes the toast (<see cref="Closing"/>).</summary>
    [RelayCommand]
    private void Close()
    {
        if (Job.IsRunning)
            Job.CancelCommand.Execute(null);
        Closing?.Invoke();
    }

    /// <summary>The toast asked to go (×).</summary>
    public event Action? Closing;

    private void OnJobChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var job = Job.Job;
        Finished = !Job.IsRunning;
        Fraction = Finished ? 1 : Math.Clamp(Job.Progress / 100, 0, 1);
        Status = DropToastLayout.Status(_loc,
            job.Sources.Count == 1 ? Path.GetFileName(job.Sources[0]) : null,
            job.Files.Count,
            Job.Progress,
            job.Files.Count(f => f.State == FileState.Done),
            Finished,
            Job.IsDownloading ? Job.DownloadText : null);
    }

    public void Dispose() => Job.PropertyChanged -= OnJobChanged;
}
