using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// The font files PDFs are written in, found once per process: the regular and bold faces of Arial, or of the
/// installed font that stands in for it. When the host has no usable TrueType font, PDFs are written in
/// Helvetica, which every PDF reader has.
/// </summary>
public sealed class PdfFonts
{
    /// <summary>
    /// The family PDFs are written in, or the installed font that stands in for it.
    /// </summary>
    public const string DefaultFamily = "Arial";

    private static readonly Lazy<PdfFonts> _installed = new(FindInstalled);

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFonts"/> class. A file PdfPig cannot write with is not
    /// used.
    /// </summary>
    /// <param name="regular">The regular TrueType file, or <see langword="null"/> to write in Helvetica.</param>
    /// <param name="bold">The bold TrueType file, or <see langword="null"/> to outline the regular face.</param>
    internal PdfFonts(
        byte[] regular,
        byte[] bold)
    {
        Regular = PdfFont.CanUse(regular) ? regular : null;
        Bold = Regular is not null && PdfFont.CanUse(bold) ? bold : null;
    }

    /// <summary>
    /// Gets the fonts installed on this machine.
    /// </summary>
    public static PdfFonts Installed => _installed.Value;

    /// <summary>
    /// Gets the regular TrueType file, or <see langword="null"/> when PDFs are written in Helvetica.
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
    public PdfFont CreateRegular(PdfDocumentBuilder builder)
    {
        return Regular is null
            ? PdfFont.FromStandard(builder, Standard14Font.Helvetica)
            : PdfFont.FromTrueType(builder, Regular, simulatesBold: false);
    }

    /// <summary>
    /// Creates the bold face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public PdfFont CreateBold(PdfDocumentBuilder builder)
    {
        if (Bold is not null)
        {
            return PdfFont.FromTrueType(builder, Bold, simulatesBold: false);
        }

        return Regular is null
            ? PdfFont.FromStandard(builder, Standard14Font.HelveticaBold)
            : PdfFont.FromTrueType(builder, Regular, simulatesBold: true);
    }

    private static PdfFonts FindInstalled()
    {
        if (!PdfFontResolver.TryCreate(out var resolver))
        {
            return new PdfFonts(null, null);
        }

        var regular = resolver.Resolve(DefaultFamily, isBold: false);
        var bold = resolver.Resolve(DefaultFamily, isBold: true);

        return new PdfFonts(Read(regular.Path), bold.SimulateBold ? null : Read(bold.Path));
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
