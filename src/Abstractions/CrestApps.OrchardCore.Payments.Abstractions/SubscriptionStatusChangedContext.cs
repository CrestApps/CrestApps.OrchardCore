namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a change to the lifecycle state of a recurring subscription hosted by a payment
/// gateway — including a cancellation performed outside the application, for example from the gateway's own
/// dashboard or by the gateway itself after exhausting dunning retries.
/// </summary>
/// <remarks>
/// Without this notification an application only learns that a subscription ended by noticing that renewal
/// payments stopped arriving, which it cannot distinguish from a delayed webhook. The gateway is authoritative
/// for the remote agreement's state; the consuming feature decides what that means locally.
/// </remarks>
public sealed class SubscriptionStatusChangedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the gateway identifier of the subscription whose state changed.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral state the gateway now reports.
    /// </summary>
    public RemoteSubscriptionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the currently paid period, when the gateway reports one. It is the date the
    /// customer's access is paid through, which a cancellation scheduled for the period end must respect.
    /// </summary>
    public DateTime? CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the subscription was canceled, when applicable.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription is set to cancel at the end of the current
    /// period rather than immediately. When <see langword="true"/> the customer keeps access until
    /// <see cref="CurrentPeriodEndUtc"/>.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }
}
