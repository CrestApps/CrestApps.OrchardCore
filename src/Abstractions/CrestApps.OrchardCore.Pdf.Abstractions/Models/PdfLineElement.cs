namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A straight line from (<see cref="PdfElement.X"/>, <see cref="PdfElement.Top"/>) to (<see cref="X2"/>,
/// <see cref="Top2"/>).
/// </summary>
public sealed class PdfLineElement : PdfElement
{
    /// <summary>
    /// Gets or sets the left of the end of the line.
    /// </summary>
    public double X2 { get; set; }

    /// <summary>
    /// Gets or sets the top of the end of the line.
    /// </summary>
    public double Top2 { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public PdfColor Color { get; set; } = PdfColor.Black;

    /// <summary>
    /// Gets or sets the width of the line, in points.
    /// </summary>
    public double Thickness { get; set; } = 1;
}
