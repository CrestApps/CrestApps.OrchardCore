using CrestApps.OrchardCore.Payments;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

public sealed class SubscriptionIndex : MapIndex
{
    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string Gateway { get; set; }

    public GatewayMode GatewayMode { get; set; }

    public string ContentType { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public string SessionId { get; set; }

    public string OwnerId { get; set; }
}
