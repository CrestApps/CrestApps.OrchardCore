namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A designed PDF document: its pages, each with the elements placed on it, and the fonts it is written in.
/// </summary>
/// <remarks>
/// Positions and sizes are in points (1/72 inch) from the top left of the page.
/// <see cref="CrestApps.OrchardCore.Pdf.IPdfDocumentRenderer"/>
/// turns the layout into a PDF file.
/// </remarks>
public sealed class PdfDocumentLayout
{
    /// <summary>
    /// Gets or sets the title written in the document information of the file.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the author written in the document information of the file.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the regular TrueType font file the text is written in, embedded as a subset of the characters the
    /// document uses; <see langword="null"/> uses the installed sans-serif font, or Helvetica.
    /// </summary>
    public byte[] RegularFont { get; set; }

    /// <summary>
    /// Gets or sets the bold TrueType font file; <see langword="null"/> outlines the regular face.
    /// </summary>
    public byte[] BoldFont { get; set; }

    /// <summary>
    /// Gets the pages, in order.
    /// </summary>
    public List<PdfPageLayout> Pages { get; } = [];
}
