using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction connection.
/// </summary>
public class EditChatInteractionConnectionViewModel
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
    /// Gets or sets the chat deployment vision support lookup keyed by deployment name.
    /// </summary>
    [BindNever]
    public Dictionary<string, bool> DeploymentVisionSupport { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether the resolved default chat deployment supports vision.
    /// </summary>
    [BindNever]
    public bool DefaultChatDeploymentSupportsVision { get; set; }

    /// <summary>
    /// Gets or sets the utility deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }
}
