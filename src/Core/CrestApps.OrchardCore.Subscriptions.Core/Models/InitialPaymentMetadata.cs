using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class InitialPaymentMetadata
{
    public double? Amount { get; set; }

    public string Currency { get; set; }

    public GatewayMode Mode { get; set; }
}
