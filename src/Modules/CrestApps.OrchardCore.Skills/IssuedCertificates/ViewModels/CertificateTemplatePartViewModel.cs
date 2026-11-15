using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;

public class CertificateTemplatePartViewModel
{
    public string PageSize { get; set; }

    public bool Landscape { get; set; }

    public string FontFamily { get; set; }

    public string AccentColor { get; set; }

    public string TextColor { get; set; }

    public bool ShowBorder { get; set; }

    public bool ShowQrCode { get; set; }

    public double QrX { get; set; }

    public double QrTop { get; set; }

    public double QrSize { get; set; }

    public List<CertificateTemplateBlock> Blocks { get; set; } = [];
}
