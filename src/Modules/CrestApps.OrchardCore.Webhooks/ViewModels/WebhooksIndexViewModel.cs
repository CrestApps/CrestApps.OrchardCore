using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Webhooks.ViewModels;

/// <summary>
/// The webhook subscriptions list.
/// </summary>
public class WebhooksIndexViewModel
{
    [BindNever]
    public AdminListContext List { get; set; }
}
