// fork: the Organize editor (Phase 5) on a three-page PDF: the cards, picking, moving, turning, copying and removing,
// and the order Apply hands the PDF engine. Screenshot for a look by eye.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Filee.App.Editors;
using Filee.App.Services;
using Filee.Core.Editing;

namespace Filee.App.Tests;

public class OrganizeEditorTests
{
    [AvaloniaFact]
    public void Pages_can_be_moved_turned_copied_and_removed()
    {
        TestServices.EnsureInitialized("en");
        var folder = Directory.CreateTempSubdirectory("flick-organize-").FullName;
        try
        {
            var file = Path.Combine(folder, "notes.pdf");
            using (var document = new PdfSharp.Pdf.PdfDocument())
            {
                for (var i = 0; i < 3; i++)
                    document.AddPage();
                document.Save(file);
            }

            var (window, model) = AppHost.Get<WindowService>().ShowEditor(Core.Wheels.EditorKind.Organize, file, "Organize", null, activate: true);
            var organize = Assert.IsType<OrganizeEditorViewModel>(model);
            Assert.True(Pump(() => organize.Pages.Count == 3));
            Assert.Equal(["Page 1", "Page 2", "Page 3"], organize.Pages.Select(p => p.Label));
            Assert.True(organize.Pages[0].IsSelected);
            Assert.False(organize.ApplyCommand.CanExecute(null));

            organize.Move(0, 2);          // 2 3 1, page 1 picked
            organize.RotateRightCommand.Execute(null);
            organize.Arrow(-1, large: false); // pick page 3
            organize.DuplicateCommand.Execute(null);
            organize.Arrow(-1, large: true);  // the copy one place earlier: 2 3 3 1
            Assert.Equal([new PagePlacement(1), new PagePlacement(2), new PagePlacement(2), new PagePlacement(0, 90)], organize.Placements);
            Assert.True(organize.ApplyCommand.CanExecute(null));

            Pump(() => false, 20);
            using (var frame = window.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, "editor-organize.png"));
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            for (var i = 0; i < 5; i++)
                organize.RemoveCommand.Execute(null);
            Assert.Single(organize.Pages); // the last page stays
            Assert.False(organize.RemoveCommand.CanExecute(null));

            var preset = new Core.Presets.Preset();
            organize.ApplyTo(preset);
            Assert.Single(preset.Pdf.Edit!.Pages);
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
