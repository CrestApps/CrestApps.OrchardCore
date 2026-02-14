using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditCopilotProfileViewModel
{
    /// <summary>
    /// The Copilot model to use for the session.
    /// </summary>
    public string CopilotModel { get; set; }

    /// <summary>
    /// Additional Copilot execution flags (e.g., --allow-all).
    /// </summary>
    public string CopilotFlags { get; set; }

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
}
