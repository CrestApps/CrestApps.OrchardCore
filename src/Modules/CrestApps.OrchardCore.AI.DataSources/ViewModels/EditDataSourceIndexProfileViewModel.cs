using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing Data Source knowledge base index profile embedding settings.
/// </summary>
public class EditDataSourceIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the selected embedding deployment name.
    /// </summary>
    public string EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the available embedding deployments.
    /// </summary>
    public IList<SelectListItem> EmbeddingDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets the selected embedding deployment label for read-only display.
    /// </summary>
    public string EmbeddingDeploymentText { get; set; }

    /// <summary>
    /// Gets or sets whether the embedding deployment is locked (read-only after creation).
    /// </summary>
    public bool IsLocked { get; set; }
}
