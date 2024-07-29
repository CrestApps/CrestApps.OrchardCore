using YesSql.Indexes;

namespace OrchardCore.CrestApps.Subscriptions.Core.Indexes;

public sealed class SubscriptionsContentItemIndex : MapIndex
{
    public string ContentType { get; set; }

    public string ContentItemId { get; set; }

    public int Sort { get; set; }
}
