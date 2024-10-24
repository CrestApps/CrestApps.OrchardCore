using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is stored in the session and contains information about a single subscription.
/// </summary>
public sealed class SubscriptionInfo
{
    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string SubscriptionId { get; set; }

    public string Gateway { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string GatewayCustomerId { get; set; }
}
