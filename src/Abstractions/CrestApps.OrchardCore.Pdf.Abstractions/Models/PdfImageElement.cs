namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A JPEG or 8-bit PNG image, stretched to a box.
/// </summary>
public sealed class PdfImageElement : PdfElement
{
    /// <summary>
    /// Gets or sets the image file. A file that is neither JPEG nor PNG is not drawn.
    /// </summary>
    public byte[] Data { get; set; }

    /// <summary>
    /// Gets or sets the width of the box, in points.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the box, in points.
    /// </summary>
    public double Height { get; set; }
}
