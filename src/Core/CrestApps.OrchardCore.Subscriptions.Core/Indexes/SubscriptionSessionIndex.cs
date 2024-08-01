using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

public class SubscriptionSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ContentItemId { get; set; }

    public string ContentItemVersionId { get; set; }

    public string OwnerId { get; set; }

    public string Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime CompletedUtc { get; set; }
}
