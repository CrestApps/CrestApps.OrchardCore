using System.Globalization;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// An opaque RGB color of a PDF.
/// </summary>
/// <param name="R">The red component.</param>
/// <param name="G">The green component.</param>
/// <param name="B">The blue component.</param>
public readonly record struct PdfColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Reads a color written as six hexadecimal digits, with or without a leading <c>#</c>, such as <c>#2563EB</c>.
    /// </summary>
    /// <param name="value">The color.</param>
    /// <param name="color">The color, when the value is one.</param>
    /// <returns><see langword="true"/> when the value is a color.</returns>
    public static bool TryParseHex(string value, out PdfColor color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = value.AsSpan().Trim();

        if (digits.Length > 0 && digits[0] == '#')
        {
            digits = digits[1..];
        }

        if (digits.Length != 6 || !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = FromRgb(number);

        return true;
    }

    /// <summary>
    /// Creates a color from its red, green and blue bytes packed in a number, such as <c>0x2563EB</c>. Higher bits are
    /// ignored.
    /// </summary>
    /// <param name="rgb">The packed color.</param>
    /// <returns>The color.</returns>
    public static PdfColor FromRgb(uint rgb)
    {
        return new PdfColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
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
