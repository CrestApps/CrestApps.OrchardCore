using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A payment method a customer kept with a provider, so it can be charged again later without them present. It is
/// recorded when a checkout that keeps the card (<see cref="CheckoutSession.SavePaymentMethod"/>) completes, and is
/// read and charged through <see cref="ISavedPaymentMethodStore"/>.
/// </summary>
public sealed class CustomerPaymentMethod
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the kept payment method.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the payment method belongs to.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the payment method as the provider kept it: its references and how it is described to a person.
    /// </summary>
    public SavedPaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the owner's default payment method, the one charged when no
    /// other is named.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that kept the payment method, when one did.
    /// </summary>
    public string SourceSessionId { get; set; }

    /// <summary>
    /// Gets or sets when the payment method was first kept.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the payment method was last kept again or changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the payment method was last charged successfully without the owner present.
    /// </summary>
    public DateTime? LastChargedUtc { get; set; }
}
