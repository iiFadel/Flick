// fork: the Organize editor (Phase 5): the PDF's pages as small pictures in a grid; drag one to move it, or pick one and
// turn it, copy it, move it, or leave it out. The document never goes empty. Apply copies the pages into a new PDF
// through the PDF engine (PdfOptions.Edit) as "name Organized.pdf".

using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Editing;
using Filee.Core.Presets;
using Filee.Engines.Pdf;
using Microsoft.Extensions.Logging;

namespace Filee.App.Editors;

/// <summary>One page in the Organize grid.</summary>
/// <param name="Position">Where it is in the new document (0-based).</param>
public sealed record PageCard(int Position, Bitmap? Picture, int Rotation, string Label, bool IsSelected);

public sealed partial class OrganizeEditorViewModel(string title, string source, ILogger? log, Func<int, string> pageLabel)
    : EditorViewModel(title, source, log)
{
    private const int ThumbnailHeight = 220;

    private readonly List<Bitmap> _pictures = [];
    private PageOrganizer _pages = new(0);

    public ObservableCollection<PageCard> Pages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RotateLeftCommand), nameof(RotateRightCommand), nameof(DuplicateCommand), nameof(RemoveCommand))]
    private int _selected = -1;

    /// <summary>The order the new document will have.</summary>
    public IReadOnlyList<PagePlacement> Placements => _pages.Pages;

    protected override bool HasChanges => !_pages.IsUnchanged;

    protected override async Task LoadPreviewAsync()
    {
        var pictures = await Task.Run(() => PdfPages.Thumbnails(Source, ThumbnailHeight, CancellationToken.None));
        foreach (var png in pictures)
        {
            using var stream = new MemoryStream(png);
            _pictures.Add(new Bitmap(stream));
        }
        _pages = new PageOrganizer(_pictures.Count);
        Selected = _pictures.Count > 0 ? 0 : -1;
        Refresh();
    }

    /// <summary>A page was dragged from <paramref name="from"/> onto <paramref name="to"/>.</summary>
    public void Move(int from, int to)
    {
        _pages.Move(from, to);
        Selected = Math.Clamp(to, 0, _pages.Pages.Count - 1);
        Refresh();
    }

    public void Select(int position)
    {
        Selected = position;
        Refresh();
    }

    /// <summary>← → pick the page before or after; with Shift the picked page moves.</summary>
    public override bool Arrow(int direction, bool large)
    {
        if (Selected < 0)
            return false;
        if (large)
            Move(Selected, Selected + direction);
        else
            Select(Math.Clamp(Selected + direction, 0, _pages.Pages.Count - 1));
        return true;
    }

    private bool HasSelection() => Selected >= 0 && Selected < _pages.Pages.Count;

    private bool CanRemove() => HasSelection() && _pages.Pages.Count > 1;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RotateLeft() => Change(() => _pages.Rotate(Selected, -1));

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RotateRight() => Change(() => _pages.Rotate(Selected, 1));

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Duplicate() => Change(() =>
    {
        _pages.Duplicate(Selected);
        Selected++;
    });

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove() => Change(() =>
    {
        if (_pages.Remove(Selected))
            Selected = Math.Min(Selected, _pages.Pages.Count - 1);
    });

    [RelayCommand]
    private void Reset() => Change(() =>
    {
        _pages = new PageOrganizer(_pictures.Count);
        Selected = _pictures.Count > 0 ? 0 : -1;
    });

    public override void ApplyTo(Preset preset) => preset.Pdf.Edit = new PdfEdit { Pages = [.. _pages.Pages] };

    private void Change(Action change)
    {
        change();
        Refresh();
    }

    private void Refresh()
    {
        Pages.Clear();
        for (var i = 0; i < _pages.Pages.Count; i++)
        {
            var page = _pages.Pages[i];
            Pages.Add(new PageCard(i, page.Source < _pictures.Count ? _pictures[page.Source] : null, page.Rotation, pageLabel(page.Source + 1), i == Selected));
        }
        RemoveCommand.NotifyCanExecuteChanged();
        ChangesChanged();
    }

    public override void Dispose()
    {
        Pages.Clear();
        foreach (var picture in _pictures)
            picture.Dispose();
        _pictures.Clear();
        base.Dispose();
    }
}
