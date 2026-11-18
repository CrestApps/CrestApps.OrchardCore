using System.Collections.Concurrent;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// The font files a PDF document is written in: the regular and bold faces of an installed family, such as Arial, or
/// of the installed font that stands in for it. They are found once per process and family. When the host has no
/// usable TrueType font, documents are written in Helvetica, which every PDF reader has.
/// </summary>
public sealed class PdfFontSet
{
    private static readonly ConcurrentDictionary<string, PdfFontSet> _installed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFontSet"/> class. A file PdfPig cannot write with is not
    /// used.
    /// </summary>
    /// <param name="regular">The regular TrueType file, or <see langword="null"/> to write in Helvetica.</param>
    /// <param name="bold">The bold TrueType file, or <see langword="null"/> to outline the regular face.</param>
    public PdfFontSet(
        byte[] regular,
        byte[] bold)
    {
        Regular = PdfFontFace.CanUse(regular) ? regular : null;
        Bold = Regular is not null && PdfFontFace.CanUse(bold) ? bold : null;
    }

    /// <summary>
    /// Gets the fonts installed on this machine for a family, or for the font that stands in for it.
    /// </summary>
    /// <param name="familyName">The family a document asks for, such as <c>Arial</c>.</param>
    /// <returns>The fonts.</returns>
    public static PdfFontSet GetInstalled(string familyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(familyName);

        return _installed.GetOrAdd(familyName, FindInstalled);
    }

    /// <summary>
    /// Gets the regular TrueType file, or <see langword="null"/> when documents are written in Helvetica.
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
    public PdfFontFace CreateRegular(PdfDocumentBuilder builder)
    {
        return Regular is null
            ? PdfFontFace.FromStandard(builder, Standard14Font.Helvetica)
            : PdfFontFace.FromTrueType(builder, Regular, simulatesBold: false);
    }

    /// <summary>
    /// Creates the bold face of a document.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <returns>The face.</returns>
    public PdfFontFace CreateBold(PdfDocumentBuilder builder)
    {
        if (Bold is not null)
        {
            return PdfFontFace.FromTrueType(builder, Bold, simulatesBold: false);
        }

        return Regular is null
            ? PdfFontFace.FromStandard(builder, Standard14Font.HelveticaBold)
            : PdfFontFace.FromTrueType(builder, Regular, simulatesBold: true);
    }

    private static PdfFontSet FindInstalled(string familyName)
    {
        if (!PdfFontResolver.TryCreate(out var resolver))
        {
            return new PdfFontSet(null, null);
        }

        var regular = resolver.Resolve(familyName, isBold: false);
        var bold = resolver.Resolve(familyName, isBold: true);

        return new PdfFontSet(Read(regular.Path), bold.SimulateBold ? null : Read(bold.Path));
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
