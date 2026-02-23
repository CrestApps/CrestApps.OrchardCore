using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;

public class CopilotSettingsViewModel
{
    public CopilotAuthenticationType AuthenticationType { get; set; }

    // ── GitHub OAuth fields ──

    public string ClientId { get; set; }

    public string ClientSecret { get; set; }

    public bool HasSecret { get; set; }

    /// <summary>
    /// The auto-computed callback URL to display to the user (read-only).
    /// </summary>
    [BindNever]
    public string ComputedCallbackUrl { get; set; }

    // ── BYOK (API Key) fields ──

    public string ProviderType { get; set; }

    public string BaseUrl { get; set; }

    public string ApiKey { get; set; }

    public bool HasApiKey { get; set; }

    public string WireApi { get; set; }

    public string DefaultModel { get; set; }

    public string AzureApiVersion { get; set; }

    [BindNever]
    public IList<SelectListItem> AuthenticationTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> ProviderTypes { get; set; }

    [BindNever]
    public IList<SelectListItem> WireApiOptions { get; set; }
}
