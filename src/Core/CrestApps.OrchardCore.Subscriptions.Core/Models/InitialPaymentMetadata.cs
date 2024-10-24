using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class InitialPaymentMetadata
{
    public string TransactionId { get; set; }

    public double? Amount { get; set; }

    public string Currency { get; set; }

    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }
}
