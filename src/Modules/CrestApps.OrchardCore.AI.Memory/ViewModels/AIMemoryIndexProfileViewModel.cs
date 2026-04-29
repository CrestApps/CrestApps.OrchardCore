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
    public IEnumerable<SelectListItem> EmbeddingDeployments { get; set; } = [];
}
