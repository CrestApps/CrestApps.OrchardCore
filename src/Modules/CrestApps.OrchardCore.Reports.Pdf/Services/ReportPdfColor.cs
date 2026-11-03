using System.Globalization;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// An opaque RGB color of the PDF.
/// </summary>
/// <param name="R">The red component.</param>
/// <param name="G">The green component.</param>
/// <param name="B">The blue component.</param>
internal readonly record struct ReportPdfColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Reads a report color, such as <c>#2563EB</c>. A translucent color is mixed with the white of the page, which
    /// is how it shows on screen.
    /// </summary>
    /// <param name="value">The color of a report style.</param>
    /// <param name="color">The color, when it is valid.</param>
    /// <returns><see langword="true"/> when the value is a color.</returns>
    public static bool TryParse(string value, out ReportPdfColor color)
    {
        color = default;

        if (!ReportColor.TryGetArgb(value, out var argb) ||
            !uint.TryParse(argb, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = new ReportPdfColor((byte)(number >> 16), (byte)(number >> 8), (byte)number)
            .Lighten(1 - ((number >> 24) / 255d));

        return true;
    }

    /// <summary>
    /// Mixes the color with white.
    /// </summary>
    /// <param name="amount">How much white, from 0 (the color) to 1 (white).</param>
    /// <returns>The lighter color.</returns>
    public ReportPdfColor Lighten(double amount)
    {
        return new ReportPdfColor(Mix(R, amount), Mix(G, amount), Mix(B, amount));
    }

    private static byte Mix(byte component, double amount)
    {
        return (byte)Math.Round(component + ((255 - component) * Math.Clamp(amount, 0, 1)));
    }
}
