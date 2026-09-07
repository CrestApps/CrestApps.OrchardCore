namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// The lifecycle state of a single purchased subscription, tracked locally so the site can answer "is this
/// customer currently entitled?" without calling the payment gateway on every request.
/// </summary>
/// <remarks>
/// This is distinct from the checkout session's own status: a session records whether the <em>purchase</em>
/// completed, while this records what happened to the subscription afterwards — renewals, failed renewals, and
/// cancellation. It is also distinct from the gateway's remote status, which is only an input: the gateway
/// reports that a renewal failed, and the site decides whether that means access ends immediately or after a
/// grace period.
/// </remarks>
public enum SubscriptionLifecycleStatus
{
    /// <summary>
    /// The subscription is active and paid up to date. This is the default so subscriptions recorded before the
    /// status existed keep their active meaning.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The subscription is in a trial period and has not been charged yet.
    /// </summary>
    Trialing = 1,

    /// <summary>
    /// A renewal payment failed. The subscription still exists and the gateway may still be retrying, so this
    /// is a state to chase the customer from rather than an ending.
    /// </summary>
    PastDue = 2,

    /// <summary>
    /// The subscription was canceled and will not renew. Access may still run to the end of the paid period.
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// The subscription reached the end of its paid period after being canceled or unpaid, and no longer
    /// entitles the customer to anything.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Billing is paused at the gateway. The agreement exists but is not being collected.
    /// </summary>
    Paused = 5,
}
