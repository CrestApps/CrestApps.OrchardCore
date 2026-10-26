using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// The font files report PDFs are written in, found once per process: the regular and bold faces of Arial, or of the
/// installed font that stands in for it. When the host has no usable TrueType font, reports are written in
/// Helvetica, which every PDF reader has.
/// </summary>
internal sealed class ReportPdfFonts
{
    private static readonly Lazy<ReportPdfFonts> _installed = new(FindInstalled);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportPdfFonts"/> class.
    /// </summary>
    /// <param name="regular">The regular TrueType file, or <see langword="null"/> to write in Helvetica.</param>
    /// <param name="bold">The bold TrueType file, or <see langword="null"/> to outline the regular face.</param>
    internal ReportPdfFonts(byte[] regular, byte[] bold)
    {
        Regular = regular;
        Bold = regular is null ? null : bold;
    }

    /// <summary>
    /// Gets the fonts installed on this machine.
    /// </summary>
    public static ReportPdfFonts Installed => _installed.Value;

    /// <summary>
    /// Gets the regular TrueType file, or <see langword="null"/> when reports are written in Helvetica.
    /// </summary>
    public byte[] Regular { get; }

    /// <summary>
    /// Gets the bold TrueType file, or <see langword="null"/> when bold outlines the regular face.
    /// </summary>
    public byte[] Bold { get; }

    /// <summary>
    /// Creates the regular face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public ReportPdfFont CreateRegular(PdfDocumentBuilder builder)
    {
        return (Regular is null ? null : ReportPdfFont.FromTrueType(builder, Regular, simulatesBold: false))
            ?? ReportPdfFont.FromStandard(builder, Standard14Font.Helvetica);
    }

    /// <summary>
    /// Creates the bold face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public ReportPdfFont CreateBold(PdfDocumentBuilder builder)
    {
        if (Regular is null)
        {
            return ReportPdfFont.FromStandard(builder, Standard14Font.HelveticaBold);
        }

        return (Bold is null ? null : ReportPdfFont.FromTrueType(builder, Bold, simulatesBold: false))
            ?? ReportPdfFont.FromTrueType(builder, Regular, simulatesBold: true)
            ?? ReportPdfFont.FromStandard(builder, Standard14Font.HelveticaBold);
    }

    private static ReportPdfFonts FindInstalled()
    {
        if (!ReportPdfFontResolver.TryCreate(out var resolver))
        {
            return new ReportPdfFonts(null, null);
        }

        var regular = Read(resolver.Resolve(ReportPdfTheme.FontFamily, isBold: false));

        if (!ReportPdfFont.CanUse(regular.File))
        {
            return new ReportPdfFonts(null, null);
        }

        var bold = Read(resolver.Resolve(ReportPdfTheme.FontFamily, isBold: true));

        return new ReportPdfFonts(regular.File, !bold.SimulateBold && ReportPdfFont.CanUse(bold.File) ? bold.File : null);
    }

    private static (byte[] File, bool SimulateBold) Read((string Path, bool SimulateBold) face)
    {
        try
        {
            return (File.ReadAllBytes(face.Path), face.SimulateBold);
        }
        catch (IOException)
        {
            return (null, face.SimulateBold);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, face.SimulateBold);
        }
    }
}
