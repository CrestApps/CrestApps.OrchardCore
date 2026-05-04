using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for edit profile deployment.
/// </summary>
public class EditProfileDeploymentViewModel
{
    /// <summary>
    /// Gets or sets the chat deployment name.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the utility deployment name.
    /// </summary>
    public string UtilityDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the show missing default chat deployment warning.
    /// </summary>
    [BindNever]
    public bool ShowMissingDefaultChatDeploymentWarning { get; set; }

    /// <summary>
    /// Gets or sets the show missing default utility deployment warning.
    /// </summary>
    [BindNever]
    public bool ShowMissingDefaultUtilityDeploymentWarning { get; set; }

    /// <summary>
    /// Gets or sets the chat deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    /// <summary>
    /// Gets or sets the utility deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }
}
