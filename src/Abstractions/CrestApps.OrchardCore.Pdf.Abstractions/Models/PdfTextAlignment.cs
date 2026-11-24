namespace CrestApps.OrchardCore.Pdf.Models;

/// <summary>
/// How the lines of a <see cref="PdfTextElement"/> sit across its width.
/// </summary>
public enum PdfTextAlignment
{
    /// <summary>
    /// Each line starts at the left of the element.
    /// </summary>
    Left,

    /// <summary>
    /// Each line is centered in the element.
    /// </summary>
    Center,

    /// <summary>
    /// Each line ends at the right of the element.
    /// </summary>
    Right,
}
