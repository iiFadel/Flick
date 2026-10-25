// fork: the Organize editor (Phase 5): the PDF's pages copied into a new PDF in the order the window set (PdfOptions.Edit),
// each turned as asked on top of its own rotation, a page used twice copied twice. Pages copy as they are (PDFsharp
// import): bookmarks and form fields stay behind, as when pages are extracted. Also the window's small page pictures
// (PDFium).

using Filee.Core.Conversion;
using Filee.Core.Editing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDFtoImage;
using SkiaSharp;

// As PdfiumConverter: PDFtoImage marks its API for the platforms PDFium ships for, which cover every desktop.
#pragma warning disable CA1416

namespace Filee.Engines.Pdf;

internal static class PdfPages
{
    public static IReadOnlyList<string> Organize(ConversionStep step, PdfEdit edit, CancellationToken ct)
    {
        if (edit.Pages.Count == 0)
            throw new InvalidOperationException("A PDF needs at least one page.");
        using var source = PdfSharpConverter.Open(step.InputPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        foreach (var placement in edit.Pages)
        {
            ct.ThrowIfCancellationRequested();
            if (placement.Source < 0 || placement.Source >= source.PageCount)
                throw new InvalidOperationException($"The PDF has no page {placement.Source + 1}.");
            var page = output.AddPage(source.Pages[placement.Source]);
            page.Rotate = ((page.Rotate + placement.Rotation) % 360 + 360) % 360;
        }
        var path = step.Output.Allocate("pdf");
        if (path is null)
            return [];
        output.Save(path);
        return [path];
    }

    /// <summary>The number of pages, and each page as a PNG at most <paramref name="height"/> pixels high.</summary>
    public static IReadOnlyList<byte[]> Thumbnails(string path, int height, CancellationToken ct)
    {
        var pdf = File.ReadAllBytes(path);
        var options = new RenderOptions(Height: height, WithAspectRatio: true, WithAnnotations: true, BackgroundColor: SKColors.White);
        var pictures = new List<byte[]>();
        foreach (var bitmap in Conversion.ToImages(pdf, null, options))
        {
            ct.ThrowIfCancellationRequested();
            using (bitmap)
            using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 90))
                pictures.Add(data.ToArray());
        }
        return pictures;
    }
}
