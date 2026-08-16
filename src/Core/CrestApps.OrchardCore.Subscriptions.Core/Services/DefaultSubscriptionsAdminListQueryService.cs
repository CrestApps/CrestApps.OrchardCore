using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.ModelBinding;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Executes filtered and paged subscription session queries for the subscriptions admin list.
/// </summary>
public sealed class DefaultSubscriptionsAdminListQueryService : ISubscriptionsAdminListQueryService
{
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSubscriptionsAdminListQueryService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query subscription sessions.</param>
    /// <param name="serviceProvider">The service provider made available to query filters.</param>
    public DefaultSubscriptionsAdminListQueryService(
        ISession session,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Queries subscription sessions using the parsed filter result and applies paging to the results.
    /// </summary>
    /// <param name="page">The one-based page number to return.</param>
    /// <param name="pageSize">The number of items per page, or zero to disable paging.</param>
    /// <param name="options">The subscription list options that contain the parsed filter result.</param>
    /// <param name="updater">The model updater supplied by the caller.</param>
    /// <returns>The matching subscription sessions and total count before paging.</returns>
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
