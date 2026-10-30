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
    /// Initializes a new instance of the <see cref="ReportPdfFonts"/> class. A file PdfPig cannot write with is not
    /// used.
    /// </summary>
    /// <param name="regular">The regular TrueType file, or <see langword="null"/> to write in Helvetica.</param>
    /// <param name="bold">The bold TrueType file, or <see langword="null"/> to outline the regular face.</param>
    internal ReportPdfFonts(
        byte[] regular,
        byte[] bold)
    {
        Regular = ReportPdfFont.CanUse(regular) ? regular : null;
        Bold = Regular is not null && ReportPdfFont.CanUse(bold) ? bold : null;
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
    /// Gets the bold TrueType file, or <see langword="null"/> when bold outlines the regular face, or is Helvetica.
    /// </summary>
    public byte[] Bold { get; }

    /// <summary>
    /// Creates the regular face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public ReportPdfFont CreateRegular(PdfDocumentBuilder builder)
    {
        return Regular is null
            ? ReportPdfFont.FromStandard(builder, Standard14Font.Helvetica)
            : ReportPdfFont.FromTrueType(builder, Regular, simulatesBold: false);
    }

    /// <summary>
    /// Creates the bold face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public ReportPdfFont CreateBold(PdfDocumentBuilder builder)
    {
        if (Bold is not null)
        {
            return ReportPdfFont.FromTrueType(builder, Bold, simulatesBold: false);
        }

        return Regular is null
            ? ReportPdfFont.FromStandard(builder, Standard14Font.HelveticaBold)
            : ReportPdfFont.FromTrueType(builder, Regular, simulatesBold: true);
    }

    private static ReportPdfFonts FindInstalled()
    {
        if (!ReportPdfFontResolver.TryCreate(out var resolver))
        {
            return new ReportPdfFonts(null, null);
        }

        var regular = resolver.Resolve(ReportPdfTheme.FontFamily, isBold: false);
        var bold = resolver.Resolve(ReportPdfTheme.FontFamily, isBold: true);

        return new ReportPdfFonts(Read(regular.Path), bold.SimulateBold ? null : Read(bold.Path));
    }

    private static byte[] Read(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read is not used, like a font PdfPig cannot write with.
            return null;
        }
    }
}
