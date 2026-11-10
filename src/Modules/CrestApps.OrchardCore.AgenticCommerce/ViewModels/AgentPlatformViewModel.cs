using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AgenticCommerce.ViewModels;

/// <summary>
/// The agent platform editor.
/// </summary>
public class AgentPlatformViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public bool Enabled { get; set; }

    public bool TestMode { get; set; }

    public string[] Protocols { get; set; } = [];

    public string OAuthClientId { get; set; }

    public string ProfileUrl { get; set; }

    public string WebhookUrl { get; set; }

    public string AllowedPaymentHandlers { get; set; }

    public string AllowedIpRanges { get; set; }

    public string AcpOrderCreatedEvent { get; set; }

    public string AcpOrderUpdatedEvent { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string ItemId { get; set; }

    [BindNever]
    public string ApiKeyHint { get; set; }

    [BindNever]
    public bool HasWebhookSecret { get; set; }

    [BindNever]
    public IList<SelectListItem> ProtocolOptions { get; set; } = [];
}
