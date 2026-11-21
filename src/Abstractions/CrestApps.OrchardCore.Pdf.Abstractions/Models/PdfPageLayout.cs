namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A page of a <see cref="PdfDocumentLayout"/>.
/// </summary>
public sealed class PdfPageLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageLayout"/> class.
    /// </summary>
    public PdfPageLayout()
        : this(PdfPageSize.Letter)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPageLayout"/> class.
    /// </summary>
    /// <param name="size">The size of the page.</param>
    public PdfPageLayout(PdfPageSize size)
    {
        Size = size;
    }

    /// <summary>
    /// Gets or sets the size of the page.
    /// </summary>
    public PdfPageSize Size { get; set; }

    /// <summary>
    /// Gets or sets the color the page is filled with before anything else; <see langword="null"/> leaves it white.
    /// </summary>
    public PdfColor? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets a JPEG or 8-bit PNG file stretched over the whole page, behind the elements.
    /// </summary>
    public byte[] BackgroundImage { get; set; }

    /// <summary>
    /// Gets the elements, drawn in order: a later element is drawn over an earlier one.
    /// </summary>
    public List<PdfElement> Elements { get; } = [];
}
