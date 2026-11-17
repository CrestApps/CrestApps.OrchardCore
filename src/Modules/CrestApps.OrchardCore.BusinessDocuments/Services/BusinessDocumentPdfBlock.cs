using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// A column of content of a business document's PDF, laid out before it is drawn so that its height is known: wrapped
/// lines of text, an image and a badge, stacked from the top, each aligned in the column's width.
/// </summary>
internal sealed class BusinessDocumentPdfBlock
{
    private const double BadgePadding = 3;

    private readonly List<Item> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentPdfBlock"/> class.
    /// </summary>
    /// <param name="width">The width of the column, in points.</param>
    public BusinessDocumentPdfBlock(double width)
    {
        Width = Math.Max(1, width);
    }

    /// <summary>
    /// Gets the width of the column, in points.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the height of the content, in points.
    /// </summary>
    public double Height { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the column has no content.
    /// </summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>
    /// Adds a paragraph, wrapped to the width; nothing when the value is empty.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="font">The face.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="color">The color.</param>
    /// <param name="alignment">Where the lines sit in the width.</param>
    /// <returns>The block.</returns>
    public BusinessDocumentPdfBlock AddText(string value, PdfFontFace font, double size, PdfColor color, BusinessDocumentAlignment alignment = BusinessDocumentAlignment.Start)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        var lineHeight = font.LineHeight * size;

        foreach (var line in PdfText.Wrap(font.Prepare(value.Trim()), font, size, Width))
        {
            _items.Add(new Item(line, font, size, color, Align(font.Measure(line, size), alignment), Height, lineHeight, null, null));
            Height += lineHeight;
        }

        return this;
    }

    /// <summary>
    /// Adds an image, scaled down to fit a box with its proportions kept.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="maximumWidth">The widest the image is drawn, in points.</param>
    /// <param name="maximumHeight">The tallest the image is drawn, in points.</param>
    /// <param name="alignment">Where the image sits in the width.</param>
    /// <returns>The block.</returns>
    public BusinessDocumentPdfBlock AddImage(PdfImage image, double maximumWidth, double maximumHeight, BusinessDocumentAlignment alignment = BusinessDocumentAlignment.Start)
    {
        if (image is null)
        {
            return this;
        }

        var scale = Math.Min(Math.Min(maximumWidth, Width) / image.Width, maximumHeight / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;

        _items.Add(new Item(null, null, 0, default, Align(width, alignment), Height, height, image, null) { ImageWidth = width });
        Height += height;

        return this;
    }

    /// <summary>
    /// Adds a badge: a line of text on a filled rectangle.
    /// </summary>
    /// <param name="label">The text.</param>
    /// <param name="font">The face.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="fill">The color of the rectangle.</param>
    /// <param name="color">The color of the text.</param>
    /// <param name="alignment">Where the badge sits in the width.</param>
    /// <returns>The block.</returns>
    public BusinessDocumentPdfBlock AddBadge(string label, PdfFontFace font, double size, PdfColor fill, PdfColor color, BusinessDocumentAlignment alignment)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return this;
        }

        var text = PdfText.Fit(font.Prepare(label.Trim()).Replace('\n', ' '), font, size, Width - (4 * BadgePadding));
        var width = font.Measure(text, size) + (4 * BadgePadding);
        var height = (font.LineHeight * size) + (2 * BadgePadding);

        _items.Add(new Item(text, font, size, color, Align(width, alignment), Height, height, null, fill) { ImageWidth = width });
        Height += height;

        return this;
    }

    /// <summary>
    /// Adds space under the content; nothing when the block is empty, so a block never starts with a space.
    /// </summary>
    /// <param name="height">The height of the space, in points.</param>
    /// <returns>The block.</returns>
    public BusinessDocumentPdfBlock AddSpace(double height)
    {
        if (!IsEmpty)
        {
            Height += height;
        }

        return this;
    }

    /// <summary>
    /// Gets the bottoms of the items, from the top of the block, where the block may be split between pages.
    /// </summary>
    /// <returns>The bottoms, in points.</returns>
    public IEnumerable<double> GetBreaks()
        => _items.Select(item => item.Top + item.Height);

    /// <summary>
    /// Draws the block.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left of the block on the page.</param>
    /// <param name="top">The top of the block on the page.</param>
    public void Draw(PdfCanvas page, double left, double top)
        => Draw(page, left, top, 0, double.MaxValue);

    /// <summary>
    /// Draws the items of a band of the block, those that start at or below its top and end above its bottom, with the
    /// top of the band at a position of the page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left of the block on the page.</param>
    /// <param name="top">Where the top of the band is drawn on the page.</param>
    /// <param name="from">The top of the band, from the top of the block.</param>
    /// <param name="to">The bottom of the band, from the top of the block.</param>
    public void Draw(PdfCanvas page, double left, double top, double from, double to)
    {
        foreach (var item in _items)
        {
            if (item.Top < from - 0.01 || item.Top + item.Height > to + 0.01)
            {
                continue;
            }

            var x = left + item.X;
            var y = top + item.Top - from;

            if (item.Image is not null)
            {
                page.DrawImage(item.Image.Data, x, y, item.ImageWidth, item.Height);
            }
            else if (item.Fill is { } fill)
            {
                page.FillRectangle(x, y, item.ImageWidth, item.Height, fill);
                page.DrawText(item.Text, item.Font, item.Size, x + (2 * BadgePadding), y + BadgePadding, item.Color);
            }
            else
            {
                page.DrawText(item.Text, item.Font, item.Size, x, y, item.Color);
            }
        }
    }

    private double Align(double width, BusinessDocumentAlignment alignment)
        => alignment switch
        {
            BusinessDocumentAlignment.End => Math.Max(0, Width - width),
            BusinessDocumentAlignment.Center => Math.Max(0, (Width - width) / 2),
            _ => 0,
        };

    private sealed record Item(string Text, PdfFontFace Font, double Size, PdfColor Color, double X, double Top, double Height, PdfImage Image, PdfColor? Fill)
    {
        public double ImageWidth { get; init; }
    }
}
