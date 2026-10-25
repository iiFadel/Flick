// fork: the Edit editor (Phase 5): a slider changes the preview, Original shows the picture as it was, Reset turns
// Apply off again, and Apply hands the sliders to the image engine. Screenshot for a look by eye.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;
using ImageMagick;

namespace Filee.App.Tests;

public class AdjustEditorTests
{
    [AvaloniaFact]
    public void Sliders_change_the_preview_and_reset_to_nothing()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-adjust-").FullName;
        try
        {
            var file = Path.Combine(folder, "photo.jpg");
            using (var image = new MagickImage("gradient:#FF8A3D-#2D6CDF", new MagickReadSettings { Width = 800, Height = 500 }))
                image.Write(file, MagickFormat.Jpeg);

            var (window, model) = AppHost.Get<WindowService>().ShowEditor(Core.Wheels.EditorKind.Adjust, file, "Edit", null, activate: true);
            var adjust = Assert.IsType<AdjustEditorViewModel>(model);
            Assert.True(Pump(() => adjust.Shown is not null));
            var original = adjust.Shown;
            Assert.False(adjust.ApplyCommand.CanExecute(null));

            adjust.Saturation = -100;
            adjust.Warmth = 40;
            Assert.True(Pump(() => adjust.Preview is not null && adjust.Rendered.IsCompleted), $"no preview: {adjust.Error} {adjust.Rendered.Exception}");
            Assert.NotSame(original, adjust.Shown);
            adjust.ShowOriginal = true;
            Assert.Same(original, adjust.Shown);
            adjust.ShowOriginal = false;
            Assert.True(adjust.ApplyCommand.CanExecute(null));

            Pump(() => false, 20);
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, "editor-adjust.png"));
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            var preset = new Core.Presets.Preset();
            adjust.ApplyTo(preset);
            Assert.Equal(new Adjustments { Saturation = -100, Warmth = 40 }, preset.Image.Edit!.Adjust);

            adjust.ResetCommand.Execute(null);
            Assert.False(adjust.ApplyCommand.CanExecute(null));
            window.Close();
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    private static bool Pump(Func<bool> done, int rounds = 300)
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
