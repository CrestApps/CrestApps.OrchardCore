using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public interface ISubscriptionsAdminListQueryService
{
    Task<SubscriptionQueryResult> QueryAsync(int page, int pageSize, ListSubscriptionOptions options, IUpdateModel updater);
}
