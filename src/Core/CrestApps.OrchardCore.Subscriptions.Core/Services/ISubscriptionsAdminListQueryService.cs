using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Executes the paged, filtered query that backs the subscriptions admin list.
/// </summary>
public interface ISubscriptionsAdminListQueryService
{
    /// <summary>
    /// Queries subscription sessions for the admin list using the supplied paging and filter options.
    /// </summary>
    /// <param name="page">The one-based page number to return.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="options">The list and filter options selected in the admin UI.</param>
    /// <param name="updater">The model updater used to bind and validate the filter options.</param>
    /// <returns>The matching page of subscription sessions.</returns>
    Task<SubscriptionQueryResult> QueryAsync(int page, int pageSize, ListSubscriptionOptions options, IUpdateModel updater);
}
