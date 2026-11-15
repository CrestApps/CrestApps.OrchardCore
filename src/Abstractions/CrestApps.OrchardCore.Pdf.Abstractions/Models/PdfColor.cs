using System.Globalization;

namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// An opaque RGB color of a PDF.
/// </summary>
/// <param name="R">The red component.</param>
/// <param name="G">The green component.</param>
/// <param name="B">The blue component.</param>
public readonly record struct PdfColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Gets black.
    /// </summary>
    public static PdfColor Black { get; } = new(0, 0, 0);

    /// <summary>
    /// Gets white.
    /// </summary>
    public static PdfColor White { get; } = new(255, 255, 255);

    /// <summary>
    /// Reads a hexadecimal color, such as <c>#2563EB</c>, <c>#25E</c> or <c>#802563EB</c> (alpha first). A translucent
    /// color is mixed with the white of the page, which is how it shows on screen.
    /// </summary>
    /// <param name="value">The color, with or without its leading <c>#</c>.</param>
    /// <param name="color">The color, when it is valid.</param>
    /// <returns><see langword="true"/> when the value is a color.</returns>
    public static bool TryParse(string value, out PdfColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim().TrimStart('#');

        hex = hex.Length switch
        {
            3 => string.Concat("FF", new string(hex[0], 2), new string(hex[1], 2), new string(hex[2], 2)),
            6 => "FF" + hex,
            8 => hex,
            _ => null,
        };

        if (hex is null || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = new PdfColor((byte)(number >> 16), (byte)(number >> 8), (byte)number)
            .Lighten(1 - ((number >> 24) / 255d));

        return true;
    }

    /// <summary>
    /// Reads a hexadecimal color, or returns a fallback when the value is not one.
    /// </summary>
    /// <param name="value">The color.</param>
    /// <param name="fallback">The color to use when the value is empty or not a color.</param>
    /// <returns>The color.</returns>
    public static PdfColor Parse(string value, PdfColor fallback)
    {
        return TryParse(value, out var color) ? color : fallback;
    }

    /// <summary>
    /// Mixes the color with white.
    /// </summary>
    /// <param name="amount">How much white, from 0 (the color) to 1 (white).</param>
    /// <returns>The lighter color.</returns>
    public PdfColor Lighten(double amount)
    {
        return new PdfColor(Mix(R, amount), Mix(G, amount), Mix(B, amount));
    }

    private static byte Mix(byte component, double amount)
    {
        return (byte)Math.Round(component + ((255 - component) * Math.Clamp(amount, 0, 1)));
    }
}
