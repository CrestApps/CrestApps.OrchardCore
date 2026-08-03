using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Represents the connection view model for the AI completion with config task activity.
/// </summary>
public class AICompletionWithConfigConnectionViewModel
{
    /// <summary>
    /// Gets or sets the orchestrator name.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment name.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the utility deployment name.
    /// </summary>
    public string UtilityDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the available orchestrators.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Orchestrators { get; set; }

    /// <summary>
    /// Gets or sets the available chat deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; }

    /// <summary>
    /// Gets or sets the available utility deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> UtilityDeployments { get; set; }
}
