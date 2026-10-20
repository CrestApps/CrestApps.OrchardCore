using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Commerce.Webhooks.ViewModels;

public class WebhooksIndexViewModel
{
    [BindNever]
    public AdminListContext List { get; set; }
}

public class WebhookEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether every event is sent, those added later included.
    /// </summary>
    public bool AllEvents { get; set; }

    public List<string> Events { get; set; } = [];

    [BindNever]
    public IReadOnlyList<IGrouping<string, CommerceWebhookEvent>> AvailableEvents { get; set; } = [];

    /// <summary>
    /// Gets or sets the signing secret, shown so it can be copied into the receiver.
    /// </summary>
    [BindNever]
    public string Secret { get; set; }

    [BindNever]
    public bool IsNew
        => string.IsNullOrEmpty(ItemId);
}

public class WebhookDeliveriesViewModel
{
    [BindNever]
    public WebhookSubscription Subscription { get; set; }

    public WebhookDeliveriesOptions Options { get; set; } = new();

    [BindNever]
    public AdminListContext List { get; set; }
}

public class WebhookDeliveriesOptions
{
    public WebhookDeliveryStatus? Status { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}
