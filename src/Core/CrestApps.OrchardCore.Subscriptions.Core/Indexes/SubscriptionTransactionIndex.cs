using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

public sealed class SubscriptionTransactionIndex : MapIndex
{
    public double Amount { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Gateway { get; set; }

    public string GatewayMode { get; set; }

    public string GatewayTransactionId { get; set; }

    public string Status { get; set; }
}
