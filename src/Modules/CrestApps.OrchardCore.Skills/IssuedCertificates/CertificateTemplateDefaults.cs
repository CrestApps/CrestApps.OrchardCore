using CrestApps.OrchardCore.Pdf.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates;

/// <summary>
/// The design a certificate is printed with when its certification has no template, and the blocks a new template
/// starts with: a US Letter page on its side.
/// </summary>
public static class CertificateTemplateDefaults
{
    /// <summary>
    /// Creates the default text blocks.
    /// </summary>
    /// <returns>The blocks.</returns>
    public static List<CertificateTemplateBlock> CreateBlocks()
    {
        return
        [
            new() { Text = "Certificate", X = 72, Top = 96, FontSize = 36, Bold = true, Accent = true },
            new() { Text = "This certifies that", X = 72, Top = 170, FontSize = 14 },
            new() { Text = "{{ Certificate.HolderName }}", X = 72, Top = 200, FontSize = 30, Bold = true },
            new() { Text = "holds the {{ Certification.Name }} certification", X = 72, Top = 262, FontSize = 16 },
            new() { Text = "{{ Certificate.Evidence }}", X = 132, Top = 298, FontSize = 11, MaxLines = 3 },
            new() { Text = "Issued {{ Certificate.IssuedOn }}{% if Certificate.ExpiresOn %}  ·  Valid until {{ Certificate.ExpiresOn }}{% endif %}", X = 72, Top = 372, FontSize = 12 },
            new() { Text = "{{ Certification.Issuer }}", X = 72, Top = 456, Width = 420, FontSize = 13, Bold = true, Alignment = PdfTextAlignment.Left, Accent = true },
            new() { Text = "{% if Certificate.Number %}Certificate No. {{ Certificate.Number }}{% endif %}", X = 72, Top = 478, Width = 420, FontSize = 10, Alignment = PdfTextAlignment.Left },
            new() { Text = "Verify this certificate at {{ Certificate.VerificationUrl }}", X = 72, Top = 520, Width = 540, FontSize = 8, Alignment = PdfTextAlignment.Left, MaxLines = 2 },
        ];
    }

    /// <summary>
    /// Creates a template part with the default design.
    /// </summary>
    /// <returns>The part.</returns>
    public static CertificateTemplatePart CreatePart()
    {
        return new CertificateTemplatePart { Blocks = CreateBlocks() };
    }
}
