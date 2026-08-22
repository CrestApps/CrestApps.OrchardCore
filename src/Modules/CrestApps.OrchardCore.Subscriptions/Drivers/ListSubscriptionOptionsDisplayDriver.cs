using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds display and editor shapes for subscription list filters and actions.
/// </summary>
public sealed class ListSubscriptionOptionsDisplayDriver : DisplayDriver<ListSubscriptionOptions>
{
    // Maintain the Options prefix for compatibility with binding.

    /// <summary>
    /// Sets the model binding prefix used by subscription list options.
    /// </summary>
    /// <param name="model">The subscription list options model.</param>
    /// <param name="htmlFieldPrefix">The generated HTML field prefix.</param>
    protected override void BuildPrefix(ListSubscriptionOptions model, string htmlFieldPrefix)
    {
        Prefix = "Options";
    }

    /// <summary>
    /// Builds thumbnail filter shapes for the subscription list.
    /// </summary>
    /// <param name="model">The subscription list options model.</param>
    /// <param name="context">The display build context.</param>
    /// <returns>The combined display result for subscription list filters.</returns>
    public override Task<IDisplayResult> DisplayAsync(ListSubscriptionOptions model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("SubscriptionsAdminFilters_Thumbnail__Status", model)
                .Location("Thumbnail", "Content:30"),
            View("SubscriptionsAdminFilters_Thumbnail__Sort", model)
                .Location("Thumbnail", "Content:40")
        );
    }

    /// <summary>
    /// Builds editor shapes for the subscription list search, actions, summary, filters, and bulk actions.
    /// </summary>
    /// <param name="model">The subscription list options model.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The combined editor display result for subscription list options.</returns>
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

    /// <summary>
    /// Updates the subscription list filter result from posted list options.
    /// </summary>
    /// <param name="model">The subscription list options model.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The updated editor display result for subscription list options.</returns>
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
