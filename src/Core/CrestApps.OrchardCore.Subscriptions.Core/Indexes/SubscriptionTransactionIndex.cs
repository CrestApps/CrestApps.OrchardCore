using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

public sealed class SubscriptionTransactionIndex : MapIndex
{
    public double Amount { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string GatewayId { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string GatewayTransactionId { get; set; }

    public PaymentStatus Status { get; set; }

    public string ContentType { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public string SessionId { get; set; }

    public string OwnerId { get; set; }
}
