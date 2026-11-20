using System.Globalization;
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Reads the colors of report styles as colors of a PDF.
/// </summary>
internal static class ReportPdfColors
{
    /// <summary>
    /// Reads a report color, such as <c>#2563EB</c>. A translucent color is mixed with the white of the page, which
    /// is how it shows on screen.
    /// </summary>
    /// <param name="value">The color of a report style.</param>
    /// <param name="color">The color, when it is valid.</param>
    /// <returns><see langword="true"/> when the value is a color.</returns>
    public static bool TryParse(string value, out PdfColor color)
    {
        color = default;

        if (!ReportColor.TryGetArgb(value, out var argb) ||
            !uint.TryParse(argb, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = new PdfColor((byte)(number >> 16), (byte)(number >> 8), (byte)number)
            .Lighten(1 - ((number >> 24) / 255d));

        return true;
    }
}
