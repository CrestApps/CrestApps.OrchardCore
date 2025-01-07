using CrestApps.OrchardCore.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

public sealed class ListSubscriptionOptionsDisplayDriver : DisplayDriver<ListSubscriptionOptions>
{
    // Maintain the Options prefix for compatibility with binding.
    protected override void BuildPrefix(ListSubscriptionOptions model, string htmlFieldPrefix)
    {
        Prefix = "Options";
    }

    public override Task<IDisplayResult> DisplayAsync(ListSubscriptionOptions model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SubscriptionsAdminFilters_Thumbnail__Status", model)
                .Location("Thumbnail", "Content:30"),
            View("SubscriptionsAdminFilters_Thumbnail__Sort", model)
                .Location("Thumbnail", "Content:40")
        );
    }

    public override Task<IDisplayResult> EditAsync(ListSubscriptionOptions model, BuildEditorContext context)
    {
        model.FilterResult.MapTo(model);

        return CombineAsync(
            Initialize<ListSubscriptionOptions>("SubscriptionsAdminListSearch", m => BuildOptionsViewModel(m, model))
                .Location("Search:10"),
            Initialize<ListSubscriptionOptions>("SubscriptionsAdminListActionBarButtons", m => BuildOptionsViewModel(m, model))
                .Location("ActionBarButtons:10"),
            Initialize<ListSubscriptionOptions>("SubscriptionsAdminListSummary", m => BuildOptionsViewModel(m, model))
                .Location("Summary:10"),
            Initialize<ListSubscriptionOptions>("SubscriptionsAdminListFilters", m => BuildOptionsViewModel(m, model))
                .Location("Actions:10.1"),
            Initialize<ListSubscriptionOptions>("SubscriptionsAdminList_Fields_BulkActions", m => BuildOptionsViewModel(m, model))
                .Location("Actions:10.1")
        );
    }

    public override Task<IDisplayResult> UpdateAsync(ListSubscriptionOptions model, UpdateEditorContext context)
    {
        // Map the incoming values from a form post to the filter result.
        model.FilterResult.MapFrom(model);

        return EditAsync(model, context);
    }

    private static void BuildOptionsViewModel(ListSubscriptionOptions m, ListSubscriptionOptions model)
    {
        m.Status = model.Status;
        m.SearchText = model.SearchText;
        m.OriginalSearchText = model.OriginalSearchText;
        m.FilterResult = model.FilterResult;
        m.Sorts = model.Sorts;
        m.Statuses = model.Statuses;
        m.StartIndex = model.StartIndex;
        m.EndIndex = model.EndIndex;
        m.TotalSubscriptions = model.TotalSubscriptions;
        m.TotalItemCount = model.TotalItemCount;
        m.OrderBy = model.OrderBy;
        m.FilterResult = model.FilterResult;
    }
}
