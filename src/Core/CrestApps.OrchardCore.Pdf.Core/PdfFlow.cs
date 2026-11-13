using System.Globalization;
using System.Text;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// Lays content out down the pages of a PDF: it holds the page being filled and the position on it, and adds a
/// page when the next block does not fit.
/// </summary>
public sealed class PdfFlow
{
    /// <summary>
    /// The margin at the top, left and right of a page, in points.
    /// </summary>
    public const double Margin = 40;

    // The footer sits in the bottom margin, this far above the bottom of the page.
    private const double FooterDistance = Margin / 2;
    private const double BottomMargin = Margin + 10;

    private readonly PdfDocumentBuilder _builder;
    private readonly double _pageWidth;
    private readonly double _pageHeight;
    private readonly List<PdfPage> _pages = [];
    private readonly Action<PdfPage> _pageStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFlow"/> class, with its first page.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <param name="pageWidth">The width of a page, in points.</param>
    /// <param name="pageHeight">The height of a page, in points.</param>
    /// <param name="regular">The regular face.</param>
    /// <param name="bold">The bold face.</param>
    public PdfFlow(
        PdfDocumentBuilder builder,
        double pageWidth,
        double pageHeight,
        PdfFont regular,
        PdfFont bold)
        : this(builder, pageWidth, pageHeight, regular, bold, pageStarted: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFlow"/> class, with its first page.
    /// </summary>
    /// <param name="builder">The document.</param>
    /// <param name="pageWidth">The width of a page, in points.</param>
    /// <param name="pageHeight">The height of a page, in points.</param>
    /// <param name="regular">The regular face.</param>
    /// <param name="bold">The bold face.</param>
    /// <param name="pageStarted">Draws on each page as it starts, before its content, such as a watermark behind the
    /// content; <see langword="null"/> to draw nothing.</param>
    public PdfFlow(
        PdfDocumentBuilder builder,
        double pageWidth,
        double pageHeight,
        PdfFont regular,
        PdfFont bold,
        Action<PdfPage> pageStarted)
    {
        _builder = builder;
        _pageWidth = pageWidth;
        _pageHeight = pageHeight;
        _pageStarted = pageStarted;
        Regular = regular;
        Bold = bold;

        NewPage();
    }

    /// <summary>
    /// Gets the regular face.
    /// </summary>
    public PdfFont Regular { get; }

    /// <summary>
    /// Gets the bold face.
    /// </summary>
    public PdfFont Bold { get; }

    /// <summary>
    /// Gets the page being filled.
    /// </summary>
    public PdfPage Page { get; private set; }

    /// <summary>
    /// Gets the left of the content, in points.
    /// </summary>
    public double Left { get; } = Margin;

    /// <summary>
    /// Gets the width of the content, in points.
    /// </summary>
    public double Width => _pageWidth - (2 * Margin);

    /// <summary>
    /// Gets the height of the content of a page, in points.
    /// </summary>
    public double PageContentHeight => _pageHeight - Margin - BottomMargin;

    /// <summary>
    /// Gets the width of a page, in points.
    /// </summary>
    public double PageWidth => _pageWidth;

    /// <summary>
    /// Gets the height of a page, in points.
    /// </summary>
    public double PageHeight => _pageHeight;

    /// <summary>
    /// Gets the number of pages started so far.
    /// </summary>
    public int PageCount => _pages.Count;

    /// <summary>
    /// Gets the position the next block starts at, in points from the top of the page.
    /// </summary>
    public double Top { get; private set; }

    /// <summary>
    /// Gets the height left on the page, in points.
    /// </summary>
    public double Remaining => _pageHeight - BottomMargin - Top;

    // Whether nothing was laid out on the page yet.
    private bool IsPageEmpty => Top <= Margin;

    /// <summary>
    /// Starts a new page.
    /// </summary>
    public void NewPage()
    {
        Page = new PdfPage(_builder.AddPage(_pageWidth, _pageHeight), _pageHeight);
        _pages.Add(Page);
        Top = Margin;
        _pageStarted?.Invoke(Page);
    }

    /// <summary>
    /// Starts a new page when a block of a height does not fit on this one, unless this one is empty: a block taller
    /// than a page starts at the top of one.
    /// </summary>
    /// <param name="height">The height of the block, in points.</param>
    public void EnsureSpace(double height)
    {
        if (height > Remaining && !IsPageEmpty)
        {
            NewPage();
        }
    }

    /// <summary>
    /// Moves down the page, past a block that was drawn or a space.
    /// </summary>
    /// <param name="height">The height, in points.</param>
    public void Advance(double height)
    {
        Top += height;
    }

    /// <summary>
    /// Moves down by a space, unless the page is empty: a page does not start with a space.
    /// </summary>
    /// <param name="height">The height of the space, in points.</param>
    public void AddSpace(double height)
    {
        if (!IsPageEmpty)
        {
            Top = Math.Min(Top + height, _pageHeight - BottomMargin);
        }
    }

    /// <summary>
    /// Measures the lines of a paragraph across the content.
    /// </summary>
    /// <param name="lines">The lines, from <see cref="PdfText.Wrap"/>.</param>
    /// <param name="font">The face.</param>
    /// <param name="size">The font size, in points.</param>
    /// <returns>The height, in points.</returns>
    public static double GetHeight(IReadOnlyCollection<string> lines, PdfFont font, double size)
    {
        return lines.Count * font.LineHeight * size;
    }

    /// <summary>
    /// Writes the lines of a paragraph across the content, moving to the next page between lines when the page is full.
    /// </summary>
    /// <param name="lines">The lines, from <see cref="PdfText.Wrap"/>.</param>
    /// <param name="font">The face.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="color">The color.</param>
    public void WriteLines(IReadOnlyList<string> lines, PdfFont font, double size, PdfColor color)
    {
        var lineHeight = font.LineHeight * size;

        foreach (var line in lines)
        {
            EnsureSpace(lineHeight);
            Page.DrawText(line, font, size, Left, Top, color);
            Advance(lineHeight);
        }
    }

    /// <summary>
    /// Writes the page numbers in the footer of every page.
    /// </summary>
    /// <param name="format">The page numbers, with <c>{0}</c> for the page and <c>{1}</c> for the number of pages.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="color">The color.</param>
    public void WriteFooters(string format, double size, PdfColor color)
    {
        var lineHeight = Regular.LineHeight * size;

        for (var index = 0; index < _pages.Count; index++)
        {
            var text = Regular.Prepare(FormatPageNumbers(format, index + 1, _pages.Count)).Replace('\n', ' ');
            var width = Regular.Measure(text, size);

            _pages[index].DrawText(text, Regular, size, _pageWidth - Margin - width, _pageHeight - FooterDistance - lineHeight, color);
        }
    }

    /// <summary>
    /// Writes the page numbers where the localized text puts <c>{0}</c> and <c>{1}</c>, in whatever order. Unlike
    /// <see cref="string.Format(string, object[])"/>, it does not fail on other braces a translation holds.
    /// </summary>
    private static string FormatPageNumbers(string format, int page, int pages)
    {
        if (string.IsNullOrEmpty(format))
        {
            format = "{0} / {1}";
        }

        return new StringBuilder(format)
            .Replace("{0}", page.ToString(CultureInfo.CurrentCulture))
            .Replace("{1}", pages.ToString(CultureInfo.CurrentCulture))
            .ToString();
    }
}
