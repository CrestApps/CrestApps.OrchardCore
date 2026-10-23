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
    /// Gets or sets the things to pay for in one checkout, when there are several, for example several outstanding
    /// transactions of one customer settled with one payment. Use it instead of <see cref="ReferenceId"/>: every entry
    /// must have the same reference type, which <see cref="ReferenceType"/> may also name, and no id may appear twice.
    /// </summary>
    /// <remarks>
    /// A list of one entry starts the same checkout as <see cref="ReferenceType"/> and <see cref="ReferenceId"/> would.
    /// With more, the session keeps them in <see cref="CheckoutSession.References"/> and its
    /// <see cref="CheckoutSession.ReferenceId"/> is empty. The handler of the reference type decides whether they can be
    /// paid together, and refuses the checkout otherwise (<see cref="CheckoutRejectedException"/>).
    /// </remarks>
    public IList<CheckoutReference> References { get; set; } = [];

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

    /// <summary>
    /// Gets or sets the local URL the customer returns to, usually the page that started the checkout. The
    /// confirmation page shows a <b>Continue</b> button to it, and the payment step and the page of a canceled or
    /// expired checkout show a <b>Back</b> link to it.
    /// </summary>
    /// <remarks>
    /// Only a local URL is accepted (see <see cref="CheckoutReturnInfo.IsLocalUrl(string)"/>): a path generated with
    /// <c>Url.Action</c> or <c>Url.RouteUrl</c>, which includes the tenant's URL prefix, or an application-relative
    /// <c>~/</c> path. Starting a checkout with any other URL throws <see cref="ArgumentException"/>, so a checkout
    /// can never send a customer to another site.
    /// </remarks>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the text of the link to <see cref="ReturnUrl"/>, for example <c>Back to my payments</c>. When
    /// empty, the pages use their own wording (<b>Continue</b> or <b>Back</b>).
    /// </summary>
    public string ReturnText { get; set; }

    /// <summary>
    /// Gets or sets the heading of the confirmation page, for example <c>Payment received</c>. When empty, the
    /// confirmation is headed "Order confirmation".
    /// </summary>
    public string Title { get; set; }
}
