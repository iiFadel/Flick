// fork: see EditorWindow.axaml. Opens centred on the screen of the drop and fades in (150 ms; Reduce animations shows
// it at once). Escape cancels, Enter applies, the header moves the window. Closing it any way cancels unless Apply
// went first.

using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Filee.App.Services;

namespace Filee.App.Editors;

public partial class EditorWindow : Window
{
    private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(150);

    public EditorWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        Closed += (_, _) =>
        {
            if (DataContext is EditorViewModel model)
            {
                model.Cancel();
                model.Dispose();
            }
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ToolTitle.Text = (DataContext as EditorViewModel)?.Title.ToUpperInvariant();
    }

    /// <summary>Shows the window centred on the screen <paramref name="near"/> is on (physical pixels).</summary>
    public void ShowNear(PixelPoint? near, bool activate)
    {
        var screen = (near is { } point ? Screens.ScreenFromPoint(point) : null) ?? Screens.Primary;
        if (screen is not null)
        {
            var scaling = OperatingSystem.IsMacOS() ? 1 : screen.Scaling;
            var area = screen.WorkingArea;
            var (width, height) = ((int)(Width * scaling), (int)(Height * scaling));
            Position = new PixelPoint(area.X + Math.Max(0, (area.Width - width) / 2), area.Y + Math.Max(0, (area.Height - height) / 2));
        }

        Card.Transitions = null;
        Card.Opacity = Motion.Enabled ? 0 : 1;
        Show();
        if (activate)
            TakeKeyboard();
        if (!Motion.Enabled)
            return;
        Card.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = FadeIn }];
        Dispatcher.UIThread.Post(() => Card.Opacity = 1, DispatcherPriority.Render);
    }

    /// <summary>The drag that opened it is over: the window takes the keyboard.</summary>
    public void TakeKeyboard()
    {
        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || DataContext is not EditorViewModel model)
            return;
        switch (e.Key)
        {
            case Key.Escape:
                model.Cancel();
                e.Handled = true;
                break;
            case Key.Enter when e.Source is not TextBox:
                model.ApplyCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
