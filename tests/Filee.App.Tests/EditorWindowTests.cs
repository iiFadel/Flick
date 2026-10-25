// fork: the editor windows' shell (Phase 5) in light and dark: the Crop editor on a picture, the tool's name in capitals,
// Apply off until the box changes. Screenshots go to TestServices.ScreenshotDirectory for a look by eye.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;
using Filee.Core.Settings;
using ImageMagick;

namespace Filee.App.Tests;

public class EditorWindowTests
{
    [AvaloniaTheory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void The_crop_editor_shows_the_picture_and_its_size(ThemeMode mode)
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-editor-").FullName;
        var store = AppHost.Get<UserDataStore>();
        var before = store.Settings.Theme.Mode;
        store.Settings.Theme.Mode = mode;
        AppHost.Get<ThemeService>().Apply(store.Settings.Theme, "en");
        Application.Current!.RequestedThemeVariant = mode == ThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            var file = Path.Combine(folder, "photo.png");
            using (var image = new MagickImage("gradient:#FF8A3D-#2D6CDF", new MagickReadSettings { Width = 640, Height = 400 }))
                image.Write(file, MagickFormat.Png);

            var (window, model) = AppHost.Get<WindowService>().ShowEditor(Core.Wheels.EditorKind.CropImage, file, "Crop", null, activate: true);
            var crop = Assert.IsType<CropEditorViewModel>(model);
            Assert.True(Pump(() => crop.PixelWidth == 640));
            Assert.Equal("CROP", window.FindControl<TextBlock>("ToolTitle")!.Text);
            Assert.Equal(("640", "400"), (crop.WidthText, crop.HeightText));
            Assert.False(crop.ApplyCommand.CanExecute(null));

            crop.CropChanged(new NormalizedRect(0.1, 0.1, 0.6, 0.7));
            Assert.True(crop.ApplyCommand.CanExecute(null));
            Pump(() => false, 10);
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, $"editor-crop-{mode.ToString().ToLowerInvariant()}.png"));
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            window.Close();
            Assert.Null(crop.Preview); // closing frees the preview
        }
        finally
        {
            store.Settings.Theme.Mode = before;
            AppHost.Get<ThemeService>().Apply(store.Settings.Theme, "en");
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    private static bool Pump(Func<bool> done, int rounds = 200)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            if (done())
                return true;
            Thread.Sleep(10);
        }
        return done();
    }
}
