using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Models;

/// <summary>
/// How issued certificates are printed: page, colors, font, the Liquid text blocks and the QR code that leads to the
/// verification page. The background image is the part's <c>BackgroundImage</c> media field.
/// </summary>
public sealed class CertificateTemplatePart : ContentPart
{
    /// <summary>
    /// Gets or sets the paper: <c>Letter</c> or <c>A4</c>.
    /// </summary>
    public string PageSize { get; set; } = "Letter";

    /// <summary>
    /// Gets or sets a value indicating whether the page lies on its side.
    /// </summary>
    public bool Landscape { get; set; } = true;

    /// <summary>
    /// Gets or sets the installed font family the certificate is written in; empty uses the default sans-serif font.
    /// </summary>
    public string FontFamily { get; set; }

    /// <summary>
    /// Gets or sets the accent color, such as <c>#1E3A8A</c>.
    /// </summary>
    public string AccentColor { get; set; } = "#1E3A8A";

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public string TextColor { get; set; } = "#111827";

    /// <summary>
    /// Gets or sets a value indicating whether a double border in the accent color frames the page.
    /// </summary>
    public bool ShowBorder { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the QR code of the verification page is printed.
    /// </summary>
    public bool ShowQrCode { get; set; } = true;

    /// <summary>
    /// Gets or sets the left of the QR code, in points.
    /// </summary>
    public double QrX { get; set; } = 640;

    /// <summary>
    /// Gets or sets the top of the QR code, in points.
    /// </summary>
    public double QrTop { get; set; } = 440;

    /// <summary>
    /// Gets or sets the size of the QR code, in points.
    /// </summary>
    public double QrSize { get; set; } = 96;

    /// <summary>
    /// Gets or sets the text blocks.
    /// </summary>
    public List<CertificateTemplateBlock> Blocks { get; set; } = [];
}
