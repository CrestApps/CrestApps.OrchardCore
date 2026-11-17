namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// An opaque RGB color of a PDF document.
/// </summary>
/// <param name="R">The red component.</param>
/// <param name="G">The green component.</param>
/// <param name="B">The blue component.</param>
public readonly record struct PdfColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Gets black.
    /// </summary>
    public static PdfColor Black => new(0, 0, 0);

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
