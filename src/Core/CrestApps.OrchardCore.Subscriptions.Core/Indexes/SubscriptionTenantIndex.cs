using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Core.Indexes;

public sealed class SubscriptionTenantIndex : MapIndex
{
    public string TenantName { get; set; }

    public string Recipe { get; set; }

    public string SessionId { get; set; }
}
