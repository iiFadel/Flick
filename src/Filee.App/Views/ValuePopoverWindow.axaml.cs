// fork: see ValuePopoverWindow.axaml. Grows in at the drop point like the drop toast (120 ms) and fades out (150 ms);
// Reduce animations shows and hides it at once. Once the drag has ended it takes the keyboard: arrows move the
// selection, Enter picks, Escape cancels, and clicking elsewhere cancels too.

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

public partial class ValuePopoverWindow : Window
{
    private static readonly TimeSpan GrowIn = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromMilliseconds(150);
    private bool _activated;

    public ValuePopoverWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        Deactivated += (_, _) =>
        {
            if (_activated)
                Model?.Cancel();
        };
    }

    private ValuePopoverViewModel? Model => DataContext as ValuePopoverViewModel;

    /// <summary>Shows the popover centred on <paramref name="drop"/> (physical pixels), growing in.</summary>
    public void ShowAt(PixelPoint drop)
    {
        Card.Transitions = null;
        Card.Opacity = 0;
        Card.RenderTransform = TransformOperations.Parse("scale(0.3)");
        Show();
        UpdateLayout();

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

    /// <summary>The drag is over: the popover takes the keyboard (and losing it cancels).</summary>
    public void TakeKeyboard()
    {
        Activate();
        Focus();
        _activated = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Model is not { } model)
            return;
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Left or Key.Up:
                model.Move(-1);
                break;
            case Key.Right or Key.Down or Key.Tab:
                model.Move(1);
                break;
            case Key.Enter or Key.Space:
                model.Apply();
                break;
            case Key.Escape:
                model.Cancel();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    /// <summary>Fades the popover out, then closes it (each popover is a new window).</summary>
    public void FadeOut()
    {
        _activated = false;
        if (!Motion.Enabled || !IsVisible)
        {
            Close();
            return;
        }
        Card.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = FadeOutTime }];
        Card.Opacity = 0;
        DispatcherTimer.RunOnce(Close, FadeOutTime);
    }
}
