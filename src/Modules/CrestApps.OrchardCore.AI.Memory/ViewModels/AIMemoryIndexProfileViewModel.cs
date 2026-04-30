using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Memory.ViewModels;

/// <summary>
/// Represents the view model for AI memory index profile.
/// </summary>
public class AIMemoryIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the embedding deployment name.
    /// </summary>
    public string EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the embedding deployments.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> EmbeddingDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected embedding deployment label for read-only display.
    /// </summary>
    [BindNever]
    public string EmbeddingDeploymentText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the embedding deployment is locked.
    /// </summary>
    [BindNever]
    public bool IsLocked { get; set; }
}
