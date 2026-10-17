using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A subscription of the person.
/// </summary>
public sealed class CommerceSubscriptionRecord
{
    /// <summary>
    /// Gets or sets the subscription identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the title of the subscription.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the owner of the subscription.
    /// </summary>
    public CustomerOwner Owner { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that started the subscription.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the payment provider that bills it.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the agreement.
    /// </summary>
    public string ProviderSubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the customer it bills.
    /// </summary>
    public string ProviderCustomerId { get; set; }

    /// <summary>
    /// Gets or sets the status of the subscription.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount billed each cycle before tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax billed each cycle.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets when the subscription was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the subscription was canceled, when it was.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }

    /// <summary>
    /// Gets or sets the name captured for a guest owner.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the email captured for a guest owner.
    /// </summary>
    public string ContactEmail { get; set; }
}
