using OrchardCore.DisplayManagement.ModelBinding;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public sealed class DefaultSubscriptionsAdminListQueryService : ISubscriptionsAdminListQueryService
{
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    public DefaultSubscriptionsAdminListQueryService(
        ISession session,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _serviceProvider = serviceProvider;
    }

    public async Task<SubscriptionQueryResult> QueryAsync(int page, int pageSize, ListSubscriptionOptions options, IUpdateModel updater)
    {
        var query = _session.Query<SubscriptionSession>();

        query = await options.FilterResult.ExecuteAsync(new SubscriptionQueryContext(_serviceProvider, query));

        // Query the count before applying pagination logic.
        var totalCount = await query.CountAsync();

        if (pageSize > 0)
        {
            if (page > 1)
            {
                query = query.Skip((page - 1) * pageSize);
            }

            query = query.Take(pageSize);
        }

        return new SubscriptionQueryResult()
        {
            Subscriptions = await query.ListAsync(),
            TotalCount = totalCount,
        };
    }
}
