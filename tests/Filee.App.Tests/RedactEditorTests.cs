// fork: the Redact editor (Phase 5) on a picture: drawing, picking, Delete, Ctrl+Z, the style, and what Apply hands the
// image engine. Screenshot for a look by eye.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;
using ImageMagick;

namespace Filee.App.Tests;

public class RedactEditorTests
{
    [AvaloniaFact]
    public void Boxes_are_drawn_picked_removed_and_taken_back()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-redact-").FullName;
        try
        {
            var file = Path.Combine(folder, "photo.png");
            using (var image = new MagickImage("gradient:#FF8A3D-#2D6CDF", new MagickReadSettings { Width = 640, Height = 400 }))
                image.Write(file, MagickFormat.Png);

            var (window, model) = AppHost.Get<WindowService>().ShowEditor(Core.Wheels.EditorKind.RedactImage, file, "Redact", null, activate: true);
            var redact = Assert.IsType<RedactEditorViewModel>(model);
            Assert.True(Pump(() => redact.Preview is not null));
            Assert.False(redact.ApplyCommand.CanExecute(null));

            redact.Draw(0.1, 0.1, 0.4, 0.3);
            redact.Draw(0.6, 0.5, 0.9, 0.9);
            Assert.Equal(2, redact.Boxes.Count);
            Assert.Equal(1, redact.Selected);
            redact.SetStyleCommand.Execute(RedactStyle.Blur);
            Assert.True(redact.IsBlur);
            Pump(() => false, 20);
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, "editor-redact.png"));
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            redact.Pick(0.2, 0.2);
            Assert.Equal(0, redact.Selected);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Assert.Equal([new NormalizedRect(0.6, 0.5, 0.30000000000000004, 0.4)], redact.Boxes);
            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Empty(redact.Boxes);
            Assert.False(redact.ApplyCommand.CanExecute(null));

            redact.Draw(0, 0, 0.5, 0.5);
            var preset = new Core.Presets.Preset();
            redact.ApplyTo(preset);
            Assert.Equal(RedactStyle.Blur, preset.Image.Edit!.RedactStyle);
            Assert.Single(preset.Image.Edit.Redactions);
            Assert.Null(preset.Media.Edit);
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
