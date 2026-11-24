namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// Something placed on a <see cref="PdfPageLayout"/>.
/// </summary>
public abstract class PdfElement
{
    /// <summary>
    /// Gets or sets the left of the element, in points from the left of the page.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the top of the element, in points from the top of the page.
    /// </summary>
    public double Top { get; set; }
}
