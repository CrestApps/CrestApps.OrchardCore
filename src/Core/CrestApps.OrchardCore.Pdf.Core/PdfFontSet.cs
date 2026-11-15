using System.Collections.Concurrent;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// The regular and bold font files a PDF is written in. <see cref="Installed"/> finds them once per process: the faces
/// of Arial, or of the installed font that stands in for it. Without a usable TrueType font, the document is written
/// in Helvetica, which every PDF reader has.
/// </summary>
public sealed class PdfFontSet
{
    /// <summary>
    /// The font family <see cref="Installed"/> looks for.
    /// </summary>
    public const string DefaultFamily = "Arial";

    private static readonly Lazy<PdfFontSet> _installed = new(FindInstalled);
    private static readonly ConcurrentDictionary<string, Lazy<PdfFontSet>> _families = new(StringComparer.OrdinalIgnoreCase);

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
        Regular = PdfFont.CanUse(regular) ? regular : null;
        Bold = Regular is not null && PdfFont.CanUse(bold) ? bold : null;
    }

    /// <summary>
    /// Gets the fonts installed on this machine.
    /// </summary>
    public static PdfFontSet Installed => _installed.Value;

    /// <summary>
    /// Gets the regular TrueType file, or <see langword="null"/> when the document is written in Helvetica.
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

    /// <summary>
    /// Finds the regular and bold faces of a font family installed on this machine, such as <c>Noto Sans</c>, once per
    /// family and process; the default family stands in for a family that is not installed.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <returns>The fonts.</returns>
    public static PdfFontSet FromInstalledFamily(string family)
    {
        if (string.IsNullOrWhiteSpace(family) || string.Equals(family.Trim(), DefaultFamily, StringComparison.OrdinalIgnoreCase))
        {
            return Installed;
        }

        return _families.GetOrAdd(family.Trim(), name => new Lazy<PdfFontSet>(() => Find(name))).Value;
    }

    private static PdfFontSet FindInstalled()
    {
        return Find(DefaultFamily);
    }

    private static PdfFontSet Find(string family)
    {
        if (!PdfFontResolver.TryCreate(out var resolver))
        {
            return new PdfFontSet(null, null);
        }

        var regular = resolver.Resolve(family, isBold: false);
        var bold = resolver.Resolve(family, isBold: true);

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
