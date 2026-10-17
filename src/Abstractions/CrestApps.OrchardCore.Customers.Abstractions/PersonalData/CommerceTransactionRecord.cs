using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A ledger transaction of the person: what they owed and paid.
/// </summary>
public sealed class CommerceTransactionRecord
{
    /// <summary>
    /// Gets or sets the transaction identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the title shown to the customer.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets where the transaction came from, for example the payment provider that created it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the owner of the transaction.
    /// </summary>
    public CustomerOwner Owner { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing the transaction is for.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing the transaction is for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that created the transaction, when there is one.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the invoice number, when one was issued.
    /// </summary>
    public string InvoiceNumber { get; set; }

    /// <summary>
    /// Gets the numbers of the receipts issued for its payments.
    /// </summary>
    public IList<string> ReceiptNumbers { get; init; } = [];

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount owed before tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax owed.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total owed including tax.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount paid so far.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the amount refunded so far.
    /// </summary>
    public decimal AmountRefunded { get; set; }

    /// <summary>
    /// Gets or sets when the transaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the transaction is due, when it has a due date.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the transaction was settled, when it was.
    /// </summary>
    public DateTime? SettledUtc { get; set; }

    /// <summary>
    /// Gets or sets the name captured for a guest owner.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the email captured for a guest owner.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the description of the payment method it is collected from automatically, for example <c>Visa •••• 4242</c>.
    /// </summary>
    public string PaymentMethodDescription { get; set; }
}
