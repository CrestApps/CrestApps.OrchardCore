using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Parses the free-text filter expression used by the subscriptions admin list into a structured query
/// against <see cref="SubscriptionSession"/>.
/// </summary>
public interface ISubscriptionAdminListFilterParser : IQueryParser<SubscriptionSession>
{
}
