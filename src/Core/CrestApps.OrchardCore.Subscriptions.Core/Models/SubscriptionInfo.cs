using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is stored in the session and contains information about a single subscription.
/// </summary>
public sealed class SubscriptionInfo
{
    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the current lifecycle state of the subscription. It defaults to
    /// <see cref="SubscriptionLifecycleStatus.Active"/> so subscriptions recorded before the status existed keep
    /// their active meaning.
    /// </summary>
    public SubscriptionLifecycleStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the subscription was canceled, when applicable.
    /// </summary>
    public DateTime? CanceledAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription is set to end when the paid period expires
    /// rather than immediately, so the customer keeps access until <see cref="ExpiresAt"/>.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the most recent renewal payment failed, when applicable. It is the point the
    /// dunning window is measured from.
    /// </summary>
    public DateTime? PastDueSinceUtc { get; set; }

    public string SubscriptionId { get; set; }

    public string Gateway { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string GatewayCustomerId { get; set; }

    public PaymentMethodInfo PaymentMethod { get; set; }

    public IList<InvoiceLineItem> LineItems { get; set; }
}
