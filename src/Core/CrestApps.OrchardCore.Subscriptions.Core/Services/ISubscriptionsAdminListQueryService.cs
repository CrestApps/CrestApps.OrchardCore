using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public interface ISubscriptionsAdminListQueryService
{
    Task<SubscriptionQueryResult> QueryAsync(int page, int pageSize, ListSubscriptionOptions options, IUpdateModel updater);
}
