using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Contributes filter terms to the subscriptions admin list query engine so features can extend the
/// available search and filter syntax.
/// </summary>
public interface ISubscriptionAdminListFilterProvider
{
    /// <summary>
    /// Registers this provider's filter terms with the query engine builder.
    /// </summary>
    /// <param name="builder">The query engine builder to configure.</param>
    void Build(QueryEngineBuilder<SubscriptionSession> builder);
}
