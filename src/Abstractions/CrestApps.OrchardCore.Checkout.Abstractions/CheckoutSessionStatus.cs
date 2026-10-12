namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The lifecycle state of a <see cref="CheckoutSession"/>. The values model the full path a checkout can
/// take, including the states that exist only while a payment provider has been engaged but has not yet
/// confirmed the outcome. Keeping these states explicit is what lets the framework recover safely instead
/// of stranding a session between "our side" and the provider.
/// </summary>
public enum CheckoutSessionStatus
{
    /// <summary>
    /// The checkout has been created and the customer is still working through the flow steps.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The customer submitted the checkout and a payment provider has been engaged, but no confirmation
    /// has been received yet. The session must never fulfill anything while in this state.
    /// </summary>
    AwaitingProvider = 1,

    /// <summary>
    /// A payment was initiated at the provider and the outcome is still pending (for example an
    /// asynchronous payment method or a hosted redirect that has not returned).
    /// </summary>
    PaymentPending = 2,

    /// <summary>
    /// Every payment obligation has been verified against the provider and the checkout is complete.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// The checkout failed (for example a declined payment or a reconciliation mismatch).
    /// </summary>
    Failed = 4,

    /// <summary>
    /// The customer or the system canceled the checkout before it completed.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// The checkout was abandoned and has passed its maximum lifetime.
    /// </summary>
    Expired = 6,
}
