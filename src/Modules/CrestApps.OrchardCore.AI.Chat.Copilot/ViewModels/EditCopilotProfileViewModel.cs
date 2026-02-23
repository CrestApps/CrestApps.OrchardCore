using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;

public class EditCopilotProfileViewModel
{
    /// <summary>
    /// The Copilot model to use for the session.
    /// </summary>
    public string CopilotModel { get; set; }

    /// <summary>
    /// Whether the Copilot session should run with --allow-all flag.
    /// </summary>
    public bool IsAllowAll { get; set; }

    /// <summary>
    /// Indicates whether the user has authenticated with GitHub.
    /// </summary>
    [BindNever]
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// The GitHub username of the authenticated user.
    /// </summary>
    [BindNever]
    public string GitHubUsername { get; set; }

    /// <summary>
    /// Available Copilot models.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableModels { get; set; }

    /// <summary>
    /// The authentication type configured at the site level.
    /// Used by views to conditionally show GitHub OAuth or BYOK UI.
    /// </summary>
    [BindNever]
    public CopilotAuthenticationType AuthenticationType { get; set; }
}
