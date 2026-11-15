namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// The size of a PDF page, in points.
/// </summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct PdfPageSize(double Width, double Height)
{
    /// <summary>
    /// Gets US Letter, 8.5 by 11 inches, portrait.
    /// </summary>
    public static PdfPageSize Letter { get; } = new(612, 792);

    /// <summary>
    /// Gets A4, 210 by 297 millimeters, portrait.
    /// </summary>
    public static PdfPageSize A4 { get; } = new(595.28, 841.89);

    /// <summary>
    /// Gets the same size turned on its side, so the longer edge is the width.
    /// </summary>
    /// <returns>The landscape size.</returns>
    public PdfPageSize Landscape()
    {
        return Width >= Height ? this : new PdfPageSize(Height, Width);
    }
}
