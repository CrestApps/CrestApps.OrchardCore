namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// Text, on one line or wrapped to a width.
/// </summary>
/// <remarks>
/// Line breaks in the text are kept. A character the font has no glyph for is written as a question mark, and an
/// invisible one is left out.
/// </remarks>
public sealed class PdfTextElement : PdfElement
{
    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the width the text wraps to and is aligned in, in points; 0 writes each line at
    /// <see cref="PdfElement.X"/> without wrapping.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the font size, in points.
    /// </summary>
    public double FontSize { get; set; } = 12;

    /// <summary>
    /// Gets or sets a value indicating whether the text is bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets or sets the color of the text.
    /// </summary>
    public PdfColor Color { get; set; } = PdfColor.Black;

    /// <summary>
    /// Gets or sets how the lines sit across <see cref="Width"/>.
    /// </summary>
    public PdfTextAlignment Alignment { get; set; }

    /// <summary>
    /// Gets or sets the most lines written; a longer text ends its last line with an ellipsis. 0 writes every line.
    /// </summary>
    public int MaxLines { get; set; }
}
