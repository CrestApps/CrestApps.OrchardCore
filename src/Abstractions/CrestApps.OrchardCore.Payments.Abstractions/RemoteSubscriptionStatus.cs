namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// The provider-neutral lifecycle state a payment gateway reports for a recurring subscription it hosts.
/// Gateways use their own vocabulary (Stripe's <c>past_due</c>, <c>unpaid</c>, <c>incomplete_expired</c>, and
/// so on); a provider adapter maps its vocabulary onto these values so consuming features never branch on a
/// gateway-specific string.
/// </summary>
/// <remarks>
/// This is the state of the <em>remote</em> billing agreement, not of the local subscription record. The local
/// record decides what a remote state means for the customer (for example whether a past-due subscription
/// still grants access during a grace period); it must never be replaced wholesale by a value from here.
/// </remarks>
public enum RemoteSubscriptionStatus
{
    /// <summary>
    /// The gateway reported a state the adapter does not recognize. Consumers must leave their local state
    /// unchanged rather than guessing.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The subscription is in a trial period and has not been charged yet.
    /// </summary>
    Trialing = 1,

    /// <summary>
    /// The subscription is active and paid up to date.
    /// </summary>
    Active = 2,

    /// <summary>
    /// A cycle payment failed and the gateway is retrying. The agreement still exists.
    /// </summary>
    PastDue = 3,

    /// <summary>
    /// The gateway exhausted its retries and stopped collecting, but has not deleted the agreement.
    /// </summary>
    Unpaid = 4,

    /// <summary>
    /// The subscription was canceled and will not be billed again.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// The subscription was created but its first payment has not been confirmed yet.
    /// </summary>
    Incomplete = 6,

    /// <summary>
    /// The first payment was never confirmed within the gateway's window, so the agreement expired.
    /// </summary>
    IncompleteExpired = 7,

    /// <summary>
    /// Collection is paused at the gateway; the agreement exists but is not being billed.
    /// </summary>
    Paused = 8,
}
