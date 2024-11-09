using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class ListSubscriptionOptions
{
    public string OriginalSearchText { get; set; }

    public string SearchText { get; set; }

    public SubscriptionSessionStatus? Status { get; set; }

    public SubscriptionOrder? OrderBy { get; set; }

    public int EndIndex { get; set; }

    [BindNever]
    public int StartIndex { get; set; }

    [BindNever]
    public int TotalSubscriptions { get; set; }

    [BindNever]
    public int TotalItemCount { get; set; }

    [ModelBinder(BinderType = typeof(SubscriptionFilterEngineModelBinder), Name = nameof(SearchText))]
    public QueryFilterResult<SubscriptionSession> FilterResult { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; }

    [BindNever]
    public List<SelectListItem> Sorts { get; set; }

    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
