using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// Writes a PDF document page by page: add the fonts the document uses, add a page, draw on its
/// <see cref="PdfCanvas"/> at positions measured in points from the top left, and build the file.
/// </summary>
/// <remarks>
/// This is the entry point for documents that place their content themselves, such as a check, a pay statement or a
/// form. A point is 1/72 of an inch.
/// </remarks>
public sealed class PdfDocumentWriter : IDisposable
{
    private readonly PdfDocumentBuilder _builder = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDocumentWriter"/> class.
    /// </summary>
    /// <param name="title">The title in the document's properties, or <see langword="null"/> for none.</param>
    public PdfDocumentWriter(string title = null)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            _builder.DocumentInformation.Title = title;
        }
    }

    /// <summary>
    /// Gets the number of pages added.
    /// </summary>
    public int PageCount { get; private set; }

    /// <summary>
    /// Adds a page.
    /// </summary>
    /// <param name="size">The size of the page.</param>
    /// <returns>The canvas to draw on the page with.</returns>
    public PdfCanvas AddPage(PdfPageSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentException("A page needs a width and a height.", nameof(size));
        }

        PageCount++;

        return new PdfCanvas(_builder.AddPage(size.Width, size.Height), size.Height);
    }

    /// <summary>
    /// Adds the regular face of a font set to the document.
    /// </summary>
    /// <param name="fonts">The font set.</param>
    /// <returns>The face.</returns>
    public PdfFontFace AddRegularFont(PdfFontSet fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        return fonts.CreateRegular(_builder);
    }

    /// <summary>
    /// Adds the bold face of a font set to the document.
    /// </summary>
    /// <param name="fonts">The font set.</param>
    /// <returns>The face.</returns>
    public PdfFontFace AddBoldFont(PdfFontSet fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        return fonts.CreateBold(_builder);
    }

    /// <summary>
    /// Adds a TrueType font file to the document, such as a font a module ships.
    /// </summary>
    /// <param name="file">The TrueType file.</param>
    /// <returns>The face, or <see langword="null"/> when the font cannot be read or written with.</returns>
    public PdfFontFace AddFont(byte[] file)
    {
        if (!PdfFontFace.CanUse(file))
        {
            return null;
        }

        return PdfFontFace.FromTrueType(_builder, file, simulatesBold: false);
    }

    /// <summary>
    /// Adds a TrueType font file that holds symbols or digits and not the letters of plain text, such as the font of
    /// the line at the bottom of a check.
    /// </summary>
    /// <param name="file">The TrueType file.</param>
    /// <param name="characters">The characters the document writes with the font.</param>
    /// <returns>The face, or <see langword="null"/> when the font cannot write the characters.</returns>
    public PdfFontFace AddSymbolFont(byte[] file, string characters)
    {
        if (!PdfFontFace.CanUseSymbols(file, characters))
        {
            return null;
        }

        return PdfFontFace.FromSymbolFont(_builder, file);
    }

    /// <summary>
    /// Adds Helvetica, a font every PDF reader has, for a document that must not depend on the fonts of the host. It
    /// writes the letters of ASCII only.
    /// </summary>
    /// <param name="bold">Whether to add the bold face.</param>
    /// <returns>The face.</returns>
    public PdfFontFace AddStandardFont(bool bold = false)
    {
        return PdfFontFace.FromStandard(_builder, bold ? Standard14Font.HelveticaBold : Standard14Font.Helvetica);
    }

    /// <summary>
    /// Builds the PDF file.
    /// </summary>
    /// <returns>The file.</returns>
    public byte[] Build()
    {
        return _builder.Build();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _builder.Dispose();
    }
}
