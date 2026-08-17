namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The canonical, well-known values for <see cref="CheckoutSession.ReferenceType"/>. The reference
/// contract is intentionally generic so any consumer can drive a checkout, but ecommerce orders use one
/// stable relationship so payment attempts and refunds can always be correlated back to the order that
/// owns them:
/// <list type="bullet">
/// <item><description><see cref="CheckoutSession.ReferenceType"/> is <see cref="Order"/>.</description></item>
/// <item><description><see cref="CheckoutSession.ReferenceId"/> is the order's stable item id.</description></item>
/// <item><description><see cref="CheckoutSession.ReferenceVersionId"/> identifies the draft or quote
/// version only when the order requires versioning; otherwise it is left empty.</description></item>
/// </list>
/// The order owns the reverse link by storing the checkout session id, and an order must never be marked
/// paid from a session status alone: payment remains authoritative in the durable payment attempt ledger.
/// </summary>
public static class CheckoutReferenceTypes
{
    /// <summary>
    /// The reference type used by ecommerce orders. The <see cref="CheckoutSession.ReferenceId"/> is the
    /// owning order's item id.
    /// </summary>
    public const string Order = "Order";
}
