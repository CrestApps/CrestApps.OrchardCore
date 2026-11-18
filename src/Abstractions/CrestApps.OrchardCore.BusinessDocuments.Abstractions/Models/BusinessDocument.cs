namespace CrestApps.OrchardCore.BusinessDocuments.Models;

/// <summary>
/// A printable business document, such as a receipt, a payment request, an invoice, an estimate or a credit note, in
/// a form every renderer reads: the HTML renderer, the PDF renderer and the <c>BusinessDocument</c> shape.
/// </summary>
/// <remarks>
/// Every value is text already written for the reader: amounts carry their currency and dates are in the reader's
/// time zone and culture. Renderers never format money or dates and never compute anything; they only lay the document
/// out and encode it. A value left <see langword="null"/> or empty is left out, and a title left <see langword="null"/>
/// is replaced by the renderer's localized default.
/// </remarks>
public sealed class BusinessDocument
{
    /// <summary>
    /// Gets or sets the kind of document, an open name the consumer registers with
    /// <see cref="BusinessDocumentServiceCollectionExtensions.AddBusinessDocumentKind"/>, such as <c>Invoice</c> or
    /// <c>Receipt</c>. It picks the template and the <c>BusinessDocument__{Kind}</c> shape alternate.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the title printed at the top, such as <c>Invoice</c>.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the number of the document, such as <c>INV-1001</c>. A draft may have none.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name of the culture the document is rendered in, such as <c>fr-FR</c>, which localizes the
    /// default labels. <see langword="null"/> renders it in the current culture.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets an optional status badge, such as <c>Paid</c>.
    /// </summary>
    public BusinessDocumentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets optional text printed across the document, such as <c>DRAFT</c> or <c>VOID</c>.
    /// </summary>
    public string Watermark { get; set; }

    /// <summary>
    /// Gets or sets the party that issues the document.
    /// </summary>
    public BusinessDocumentParty Issuer { get; set; }

    /// <summary>
    /// Gets or sets the party the document is addressed to.
    /// </summary>
    public BusinessDocumentParty Recipient { get; set; }

    /// <summary>
    /// Gets or sets an optional party goods are shipped to.
    /// </summary>
    public BusinessDocumentParty ShipTo { get; set; }

    /// <summary>
    /// Gets or sets the title of the recipient block. <see langword="null"/> prints the renderer's localized
    /// "Bill to".
    /// </summary>
    public string RecipientTitle { get; set; }

    /// <summary>
    /// Gets or sets the title of the ship-to block. <see langword="null"/> prints the renderer's localized "Ship to".
    /// </summary>
    public string ShipToTitle { get; set; }

    /// <summary>
    /// Gets or sets the labelled values printed near the top, such as the dates, a reference and the terms.
    /// </summary>
    public IList<BusinessDocumentField> Fields { get; set; } = [];

    /// <summary>
    /// Gets or sets the columns of the line table.
    /// </summary>
    public IList<BusinessDocumentColumn> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows of the line table.
    /// </summary>
    public IList<BusinessDocumentRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the totals printed under the line table, such as the subtotal, the taxes, the total and the amount
    /// due.
    /// </summary>
    public IList<BusinessDocumentTotal> Totals { get; set; } = [];

    /// <summary>
    /// Gets or sets the titled text blocks printed after the totals, such as notes, terms and payment instructions.
    /// </summary>
    public IList<BusinessDocumentSection> Sections { get; set; } = [];

    /// <summary>
    /// Gets or sets the text printed at the bottom.
    /// </summary>
    public string Footer { get; set; }

    /// <summary>
    /// Gets or sets the address of the issuer's logo. The PDF renderer reads only a PNG or JPEG file in the site's media
    /// library.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the accent color, written <c>#RRGGBB</c>. Any other value is ignored.
    /// </summary>
    public string AccentColor { get; set; }

    /// <summary>
    /// Gets or sets the paper size the document is printed on.
    /// </summary>
    public BusinessDocumentPaperSize PaperSize { get; set; }
}
