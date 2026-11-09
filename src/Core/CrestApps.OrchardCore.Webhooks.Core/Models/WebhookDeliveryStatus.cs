namespace CrestApps.OrchardCore.Webhooks.Core.Models;

/// <summary>
/// Where a webhook delivery stands.
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>
    /// Waiting to be sent, or to be sent again.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The receiver answered with a 2xx status.
    /// </summary>
    Delivered = 1,

    /// <summary>
    /// Every attempt failed, or the host was refused; it is not tried again unless sent again by hand.
    /// </summary>
    Failed = 2,
}
