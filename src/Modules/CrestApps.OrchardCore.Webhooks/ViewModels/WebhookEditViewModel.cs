using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Webhooks.ViewModels;

/// <summary>
/// The webhook subscription editor.
/// </summary>
public class WebhookEditViewModel
{
    [BindNever]
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool AllEvents { get; set; }

    public List<string> Events { get; set; } = [];

    [BindNever]
    public IReadOnlyList<IGrouping<string, WebhookEventDescriptor>> AvailableEvents { get; set; } = [];

    [BindNever]
    public string Secret { get; set; }

    [BindNever]
    public bool IsNew
        => string.IsNullOrEmpty(ItemId);
}
