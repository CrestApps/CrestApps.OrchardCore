using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscription sessions displayed in the admin list.
/// </summary>
public class ListSubscriptionsViewModel
{
    /// <summary>
    /// Gets or sets the filtering, sorting, and paging options for the subscription list.
    /// </summary>
    public ListSubscriptionOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the subscription row shapes to display.
    /// </summary>
    [BindNever]
    public IEnumerable<dynamic> Subscriptions { get; set; }

    /// <summary>
    /// Gets or sets the header shape for the subscription list.
    /// </summary>
    [BindNever]
    public dynamic Header { get; set; }

    /// <summary>
    /// Gets or sets the pager shape for the subscription list.
    /// </summary>
    [BindNever]
    public dynamic Pager { get; set; }
}
