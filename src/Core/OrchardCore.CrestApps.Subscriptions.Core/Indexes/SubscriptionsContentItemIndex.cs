using YesSql.Indexes;

namespace OrchardCore.CrestApps.Subscriptions.Core.Indexes;

public sealed class SubscriptionsContentItemIndex : MapIndex
{
    public string ContentType { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public int Order { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}
