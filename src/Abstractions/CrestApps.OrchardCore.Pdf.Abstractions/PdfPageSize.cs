namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// The size of a page of a PDF document, in points (1/72 of an inch).
/// </summary>
/// <param name="Width">The width of the page.</param>
/// <param name="Height">The height of the page.</param>
public readonly record struct PdfPageSize(double Width, double Height)
{
    /// <summary>
    /// The number of points in an inch.
    /// </summary>
    public const double PointsPerInch = 72;

    /// <summary>
    /// Gets US Letter, 8.5 by 11 inches.
    /// </summary>
    public static PdfPageSize Letter => new(612, 792);

    /// <summary>
    /// Gets US Legal, 8.5 by 14 inches.
    /// </summary>
    public static PdfPageSize Legal => new(612, 1008);

    /// <summary>
    /// Gets A4, 210 by 297 millimeters.
    /// </summary>
    public static PdfPageSize A4 => new(595.276, 841.89);

    /// <summary>
    /// Gets the same page turned on its side.
    /// </summary>
    public PdfPageSize Landscape => new(Math.Max(Width, Height), Math.Min(Width, Height));

    /// <summary>
    /// Returns a page size given in inches.
    /// </summary>
    /// <param name="width">The width, in inches.</param>
    /// <param name="height">The height, in inches.</param>
    /// <returns>The page size.</returns>
    public static PdfPageSize FromInches(double width, double height)
        => new(width * PointsPerInch, height * PointsPerInch);
}
