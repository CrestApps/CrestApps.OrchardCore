using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public interface ISubscriptionAdminListFilterProvider
{
    void Build(QueryEngineBuilder<SubscriptionSession> builder);
}
