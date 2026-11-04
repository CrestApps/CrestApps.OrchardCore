using CrestApps.OrchardCore.Webhooks.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Webhooks.ViewModels;

/// <summary>
/// A webhook subscription's delivery log.
/// </summary>
public class WebhookDeliveriesViewModel
{
    [BindNever]
    public WebhookSubscription Subscription { get; set; }

    public WebhookDeliveriesOptions Options { get; set; } = new();

    [BindNever]
    public AdminListContext List { get; set; }
}
