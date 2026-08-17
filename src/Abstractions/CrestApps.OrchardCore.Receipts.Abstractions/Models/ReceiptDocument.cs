namespace CrestApps.OrchardCore.Receipts.Models;

/// <summary>
/// A fully-resolved, printable receipt. It combines the purchase data supplied by a consumer through a
/// <see cref="ReceiptRequest"/> with the issuer branding merged from <see cref="ReceiptSettings"/>, and it
/// is the model rendered by the reusable printable receipt view.
/// </summary>
public sealed class ReceiptDocument
{
    /// <summary>
    /// Gets or sets the heading printed at the top of the receipt (for example "Payment receipt").
    /// </summary>
    public string HeaderTitle { get; set; }

    /// <summary>
    /// Gets or sets the issuing business name printed on the receipt.
    /// </summary>
    public string BusinessName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the issuing business logo, when configured.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the issuing business postal address, printed as free-form text.
    /// </summary>
    public string BusinessAddress { get; set; }

    /// <summary>
    /// Gets or sets the issuing business contact email address.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the issuing business contact phone number.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the issuing business website.
    /// </summary>
    public string Website { get; set; }

    /// <summary>
    /// Gets or sets the footer text printed at the bottom of the receipt.
    /// </summary>
    public string FooterText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test-payment badge is rendered when the payment ran in a
    /// gateway test mode.
    /// </summary>
    public bool ShowTestBadge { get; set; }

    /// <summary>
    /// Gets or sets the display name of the party the receipt is billed to.
    /// </summary>
    public string BilledToName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the party the receipt is billed to.
    /// </summary>
    public string BilledToEmail { get; set; }

    /// <summary>
    /// Gets or sets the reference printed on the receipt, such as the transaction identifier.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets a short label describing what the receipt is for.
    /// </summary>
    public string SourceLabel { get; set; }

    /// <summary>
    /// Gets or sets the date and time printed on the receipt, in the time zone the consumer chose.
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code used for every amount on the receipt.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the billable line items.
    /// </summary>
    public IList<ReceiptLineItem> LineItems { get; set; } = [];

    /// <summary>
    /// Gets or sets the amount due before tax, in the receipt currency. Computed as
    /// <see cref="Total"/> minus <see cref="TaxAmount"/> when the document is built.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Gets or sets the tax lines that explain <see cref="TaxAmount"/>.
    /// </summary>
    public IList<ReceiptTaxLine> TaxLines { get; set; } = [];

    /// <summary>
    /// Gets or sets the total tax charged, in the receipt currency.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the grand total charged, including tax, in the receipt currency.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets the settled state of the payment.
    /// </summary>
    public ReceiptStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the payment was processed in a gateway test mode.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment gateway that processed the payment, for reference.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets optional free-form notes printed on the receipt.
    /// </summary>
    public string Notes { get; set; }
}
