namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// A rectangle, filled, outlined or both.
/// </summary>
public sealed class PdfRectangleElement : PdfElement
{
    /// <summary>
    /// Gets or sets the width, in points.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the height, in points.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Gets or sets the fill; <see langword="null"/> leaves the inside clear.
    /// </summary>
    public PdfColor? Fill { get; set; }

    /// <summary>
    /// Gets or sets the color of the outline; <see langword="null"/> draws no outline.
    /// </summary>
    public PdfColor? Outline { get; set; }

    /// <summary>
    /// Gets or sets the width of the outline, in points.
    /// </summary>
    public double OutlineThickness { get; set; } = 1;
}
