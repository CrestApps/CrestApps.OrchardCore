namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A QR code, drawn as vector squares so it stays sharp at any zoom, with its quiet zone on a white square.
/// </summary>
public sealed class PdfQrCodeElement : PdfElement
{
    /// <summary>
    /// Gets or sets the value the code holds, such as a URL.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the width and height of the code, quiet zone included, in points.
    /// </summary>
    public double Size { get; set; } = 72;

    /// <summary>
    /// Gets or sets the color of the dark modules.
    /// </summary>
    public PdfColor Color { get; set; } = PdfColor.Black;
}
