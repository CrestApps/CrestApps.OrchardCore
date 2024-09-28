using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// This class is used in the cached payment session to tracked a payment for each subscription.
/// </summary>
public class SubscriptionPaymentMetadata
{
    public double? Amount { get; set; }

    public string Currency { get; set; }

    public string SubscriptionId { get; set; }

    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }
}
