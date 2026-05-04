using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI provider connection fields.
/// </summary>
public class AIProviderConnectionFieldsViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment name.
    /// </summary>
    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the embedding deployment name.
    /// </summary>
    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the images deployment name.
    /// </summary>
    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string ImagesDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the utility deployment name.
    /// </summary>
    [Obsolete("Deployment names are now managed through AIDeployment entities. Retained for backward compatibility.")]
    public string UtilityDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }
}
