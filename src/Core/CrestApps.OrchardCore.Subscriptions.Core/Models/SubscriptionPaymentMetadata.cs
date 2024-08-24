using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class SubscriptionPaymentMetadata
{
    public double? Amount { get; set; }

    public string Currency { get; set; }

    public string SubscriptionId { get; set; }

    public GatewayMode Mode { get; set; }
}
