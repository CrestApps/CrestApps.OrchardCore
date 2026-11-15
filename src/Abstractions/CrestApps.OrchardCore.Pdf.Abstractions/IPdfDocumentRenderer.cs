using CrestApps.OrchardCore.Pdf.Models;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// Turns a <see cref="PdfDocumentLayout"/> into a PDF file.
/// </summary>
public interface IPdfDocumentRenderer
{
    /// <summary>
    /// Renders a layout.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <returns>The bytes of the PDF file.</returns>
    byte[] Render(PdfDocumentLayout layout);
}
