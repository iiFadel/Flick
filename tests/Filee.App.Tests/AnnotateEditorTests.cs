// fork: the Annotate editor (Phase 5) on a picture: drawing with each tool, picking, recolouring, editing text, undo and
// redo, and what Apply hands the image engine. Screenshot for a look by eye.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;
using ImageMagick;

namespace Filee.App.Tests;

public class AnnotateEditorTests
{
    [AvaloniaFact]
    public void Marks_are_drawn_picked_changed_and_taken_back()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-annotate-").FullName;
        try
        {
            var file = Path.Combine(folder, "photo.jpg");
            using (var image = new MagickImage("gradient:#F5F6F6-#B6BFB3", new MagickReadSettings { Width = 800, Height = 500 }))
                image.Write(file, MagickFormat.Jpeg);

            var (window, model) = AppHost.Get<WindowService>().ShowEditor(Core.Wheels.EditorKind.Annotate, file, "Annotate", null, activate: true);
            var annotate = Assert.IsType<AnnotateEditorViewModel>(model);
            Assert.True(Pump(() => annotate.Preview is not null));
            Assert.False(annotate.ApplyCommand.CanExecute(null));

            annotate.Drag(0.1, 0.8, 0.4, 0.5);                    // an arrow
            annotate.SetToolCommand.Execute(AnnotationKind.Rectangle);
            annotate.SetColorCommand.Execute("#E5484D");
            annotate.Drag(0.5, 0.1, 0.9, 0.45);                   // a red box
            annotate.SetToolCommand.Execute(AnnotationKind.Callout);
            annotate.Click(0.45, 0.55);
            annotate.Click(0.7, 0.7);
            annotate.SetToolCommand.Execute(AnnotationKind.Text);
            annotate.Click(0.05, 0.1);
            Assert.True(annotate.SelectedIsText);
            annotate.Text = "Look here";
            Assert.Equal([0, 0, 1, 2, 0], annotate.Marks.Select(m => m.Number));
            Assert.Equal("Look here", annotate.Marks[4].Mark.Text);

            Pump(() => false, 20);
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, "editor-annotate.png"));
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            annotate.SetToolCommand.Execute(AnnotationKind.Arrow);
            annotate.Click(0.45, 0.55);                           // picks the first callout
            Assert.Equal(2, annotate.Selected);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Assert.Equal([0, 0, 1, 0], annotate.Marks.Select(m => m.Number)); // the other callout is 1 now
            window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
            Assert.Equal(5, annotate.Marks.Count);
            window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
            Assert.Equal(4, annotate.Marks.Count);

            var preset = new Core.Presets.Preset();
            annotate.ApplyTo(preset);
            Assert.Equal(4, preset.Image.Edit!.Annotations.Count);
            Assert.Equal("#E5484D", preset.Image.Edit.Annotations[1].Mark.Color);
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
