// fork: the Tools wheel's Metadata for PDFs (PdfOptions.RemoveMetadata): the document information and every XMP
// packet go, the pages stay as they are.

using PdfSharp.Pdf;

namespace Filee.Engines.Pdf;

internal static class PdfMetadataRemover
{
    /// <summary>Removes the metadata of <paramref name="document"/>; save it afterwards.</summary>
    public static void Remove(PdfDocument document, CancellationToken cancellationToken)
    {
        // PDFsharp writes its own producer and dates when it saves; title, author, subject, keywords and creator go.
        document.Info.Elements.Clear();
        document.Internals.Catalog.Elements.Remove("/Metadata");
        document.Internals.Catalog.Elements.Remove("/PieceInfo");
        foreach (var item in document.Internals.GetAllObjects())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is PdfDictionary dictionary)
            {
                dictionary.Elements.Remove("/Metadata");
                dictionary.Elements.Remove("/PieceInfo");
            }
        }
    }
}
