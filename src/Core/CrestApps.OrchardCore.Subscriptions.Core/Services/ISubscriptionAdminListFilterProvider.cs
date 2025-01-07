using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public interface ISubscriptionAdminListFilterProvider
{
    void Build(QueryEngineBuilder<SubscriptionSession> builder);
}
