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

    /// <summary>
    /// Gets or sets what the buyer chose, when the product is offered on more than one set of terms.
    /// </summary>
    public CheckoutPriceSelection PriceSelection { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the checkout is for, when that is not the user making the
    /// request. An administrator taking a payment on a customer's behalf, and a background charge that has no
    /// request at all, both name the customer here so the session, its attempts and what it settles belong to
    /// the customer. When empty, the session belongs to the signed-in user or, failing that, to the guest
    /// browser that started it.
    /// </summary>
    /// <remarks>
    /// Only server code decides this value. It must never be taken from a request a customer can shape, because
    /// it decides whose checkout is being paid.
    /// </remarks>
    public string OwnerId { get; set; }
}
