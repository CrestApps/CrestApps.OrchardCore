namespace CrestApps.OrchardCore.Receipts.Models;

/// <summary>
/// The data a consumer supplies to build a receipt. It carries only what the consumer knows about the
/// purchase; the issuer details (business name, logo, address, and footer) are merged from the configured
/// <see cref="ReceiptSettings"/> when the document is built, so a consumer never has to know how the site
/// is branded.
/// </summary>
public sealed class ReceiptRequest
{
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
    /// Gets or sets a short label describing what the receipt is for (for example "Subscription payment").
    /// </summary>
    public string SourceLabel { get; set; }

    /// <summary>
    /// Gets or sets the date and time to print on the receipt. Callers pass the value already converted to
    /// the time zone they want displayed, so the Receipts module never has to make a localization decision.
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
    /// Gets or sets a value indicating whether the payment was processed in a gateway test mode. When
    /// <see langword="true"/> the built document may render a test-payment badge.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment gateway that processed the payment, for reference.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Gets or sets optional free-form notes to print on the receipt.
    /// </summary>
    public string Notes { get; set; }
}
