using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;

public class CopilotSettingsViewModel
{
    public string ClientId { get; set; }

    public string ClientSecret { get; set; }

    public bool HasSecret { get; set; }

    /// <summary>
    /// The auto-computed callback URL to display to the user (read-only).
    /// </summary>
    [BindNever]
    public string ComputedCallbackUrl { get; set; }
}
