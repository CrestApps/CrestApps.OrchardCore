namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A refund given back to the person, from the durable refund ledger.
/// </summary>
public sealed class CommerceRefundRecord
{
    /// <summary>
    /// Gets or sets the refund identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the refunded payment was taken in.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment attempt that was refunded.
    /// </summary>
    public string PaymentId { get; set; }

    /// <summary>
    /// Gets or sets the key of the payment provider.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference for the refund.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets the status of the refund.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount refunded, tax included.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax refunded.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the reason given for the refund.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the refund was requested.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the refund completed, when it did.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
