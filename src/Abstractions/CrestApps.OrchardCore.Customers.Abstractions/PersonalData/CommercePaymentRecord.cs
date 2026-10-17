namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A payment taken from the person, from the durable payment ledger.
/// </summary>
public sealed class CommercePaymentRecord
{
    /// <summary>
    /// Gets or sets the payment attempt identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the payment was taken in.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's transaction identifier, when the payment was confirmed.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the payment provider.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference for the payment.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets the kind of thing the payment was for.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing the payment was for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the state of the payment.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount confirmed by the provider, or the amount expected when nothing was confirmed.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax in the amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets when the payment was begun.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the payment last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
