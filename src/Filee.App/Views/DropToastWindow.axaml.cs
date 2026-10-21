// fork: see DropToastWindow.axaml. Grows in at the drop point (30 % → 100 % with a fade, 120 ms) and fades out
// (150 ms); Reduce animations shows and hides it at once.

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;

namespace Filee.App.Views;

public partial class DropToastWindow : Window
{
    private static readonly TimeSpan GrowIn = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromMilliseconds(150);
    private int _shows; // a fade-out from before the toast showed again must not hide it

    public DropToastWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        // A click on the card (not on ×) opens the folder of what was saved, as the progress ring did.
        Card.PointerPressed += (_, e) =>
        {
            if (DataContext is DropToastViewModel { Job.HasOutputs: true } model)
            {
                model.Job.OpenFolderCommand.Execute(null);
                e.Handled = true;
            }
        };
    }

    /// <summary>Shows the toast centred on <paramref name="drop"/> (physical pixels), growing in.</summary>
    public void ShowAt(PixelPoint drop)
    {
        _shows++;
        Card.Transitions = null;
        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("scale(0.3)");
        Show();
        UpdateLayout(); // sized to its content now, before it is placed by that size

        var screen = Screens.ScreenFromPoint(drop) ?? Screens.Primary;
        var scaling = OperatingSystem.IsMacOS() ? 1 : screen?.Scaling ?? 1;
        var (width, height) = ((int)Math.Ceiling(Bounds.Width * scaling), (int)Math.Ceiling(Bounds.Height * scaling));
        var area = screen?.WorkingArea ?? new PixelRect(drop.X - width, drop.Y - height, width * 2, height * 2);
        var (x, y) = DropToastLayout.Position(drop.X, drop.Y, width, height, area.X, area.Y, area.Width, area.Height);
        Position = new PixelPoint(x, y);

        if (Motion.Enabled)
            Card.Transitions =
            [
                new DoubleTransition { Property = OpacityProperty, Duration = GrowIn },
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = GrowIn, Easing = new CubicEaseOut() },
            ];
        Dispatcher.UIThread.Post(() =>
        {
            Card.Opacity = 1;
            Card.RenderTransform = TransformOperations.Parse("scale(1)");
        }, DispatcherPriority.Render);
    }

    /// <summary>Fades the toast out, then hides it and calls <paramref name="done"/>.</summary>
    public void FadeOut(Action done)
    {
        var shows = _shows;
        void Finish()
        {
            if (shows == _shows)
                Hide();
            done();
        }

        if (!Motion.Enabled || !IsVisible)
        {
            Finish();
            return;
        }
        Card.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = FadeOutTime }];
        Card.Opacity = 0;
        DispatcherTimer.RunOnce(Finish, FadeOutTime);
    }
}
