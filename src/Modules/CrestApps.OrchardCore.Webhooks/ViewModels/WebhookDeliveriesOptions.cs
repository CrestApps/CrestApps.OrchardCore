using CrestApps.OrchardCore.Webhooks.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Webhooks.ViewModels;

/// <summary>
/// The delivery log's filter.
/// </summary>
public class WebhookDeliveriesOptions
{
    public WebhookDeliveryStatus? Status { get; set; }

    [BindNever]
    public List<SelectListItem> Statuses { get; set; } = [];
}
