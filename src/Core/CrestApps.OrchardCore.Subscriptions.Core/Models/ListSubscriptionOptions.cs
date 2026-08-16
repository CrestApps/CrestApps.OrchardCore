using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Contains filtering, sorting, paging, and display state for the subscriptions admin list.
/// </summary>
public class ListSubscriptionOptions
{
    /// <summary>
    /// Gets or sets the original search text before filters are mapped or normalized.
    /// </summary>
    public string OriginalSearchText { get; set; }

    /// <summary>
    /// Gets or sets the current search text used to filter subscriptions.
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Gets or sets the selected subscription session status filter.
    /// </summary>
    public SubscriptionSessionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the selected sort order.
    /// </summary>
    public SubscriptionOrder? OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the one-based index of the last item shown on the current page.
    /// </summary>
    public int EndIndex { get; set; }

    /// <summary>
    /// Gets or sets the one-based index of the first item shown on the current page.
    /// </summary>
    [BindNever]
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the total number of subscriptions that match the current query.
    /// </summary>
    [BindNever]
    public int TotalSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the total number of items before paging is applied.
    /// </summary>
    [BindNever]
    public int TotalItemCount { get; set; }

    /// <summary>
    /// Gets or sets the parsed filter result used to build the subscriptions query.
    /// </summary>
    [ModelBinder(BinderType = typeof(SubscriptionFilterEngineModelBinder), Name = nameof(SearchText))]
    public QueryFilterResult<SubscriptionSession> FilterResult { get; set; }

    /// <summary>
    /// Gets or sets the status filter options displayed in the admin list.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Statuses { get; set; }

    /// <summary>
    /// Gets or sets the sort options displayed in the admin list.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Sorts { get; set; }

    /// <summary>
    /// Gets or sets the route values used to keep filters and paging in generated links.
    /// </summary>
    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
