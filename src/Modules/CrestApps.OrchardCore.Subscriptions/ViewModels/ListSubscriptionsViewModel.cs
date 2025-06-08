using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class ListSubscriptionsViewModel
{
    public ListSubscriptionOptions Options { get; set; }

    [BindNever]
    public IEnumerable<dynamic> Notifications { get; set; }

    [BindNever]
    public dynamic Header { get; set; }

    [BindNever]
    public dynamic Pager { get; set; }
}
