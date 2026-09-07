namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Describes what a new checkout is for. The reference triple is provider- and domain-neutral so the same
/// engine drives a subscription signup, an outstanding-balance settlement, and a future order without any of
/// them appearing in the checkout contracts.
/// </summary>
public sealed class StartCheckoutRequest
{
    /// <summary>
    /// Gets or sets the kind of thing being purchased, for example <see cref="CheckoutReferenceTypes.Order"/>.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing being purchased.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets an optional secondary identifier, for example a draft or quote version.
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// Gets or sets the contact captured for a guest buyer. A guest has no account to resolve an address or an
    /// email from, so without this a completed guest purchase cannot be receipted or chased for an outstanding
    /// balance.
    /// </summary>
    public CheckoutContactInfo Contact { get; set; }
}
