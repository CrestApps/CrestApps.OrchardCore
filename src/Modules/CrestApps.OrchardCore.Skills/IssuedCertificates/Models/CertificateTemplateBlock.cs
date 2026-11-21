using CrestApps.OrchardCore.Pdf.Models;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Models;

/// <summary>
/// A block of text on a certificate template, written in Liquid.
/// </summary>
public sealed class CertificateTemplateBlock
{
    /// <summary>
    /// Gets or sets the Liquid text, such as <c>{{ Certificate.HolderName }}</c>.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the left of the block, in points from the left of the page.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the top of the block, in points from the top of the page.
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// Gets or sets the width the text wraps to; 0 spans the page between margins of <see cref="X"/> on both sides.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the font size, in points.
    /// </summary>
    public double FontSize { get; set; } = 14;

    /// <summary>
    /// Gets or sets a value indicating whether the text is bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets or sets how the lines sit across the width.
    /// </summary>
    public PdfTextAlignment Alignment { get; set; } = PdfTextAlignment.Center;

    /// <summary>
    /// Gets or sets a value indicating whether the block takes the accent color instead of the text color.
    /// </summary>
    public bool Accent { get; set; }

    /// <summary>
    /// Gets or sets the most lines written; 0 writes every line.
    /// </summary>
    public int MaxLines { get; set; }
}
