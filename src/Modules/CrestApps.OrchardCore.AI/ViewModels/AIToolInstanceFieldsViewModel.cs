namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the shared fields captured for every AI tool instance regardless of its source.
/// </summary>
public class AIToolInstanceFieldsViewModel
{
    /// <summary>
    /// Gets or sets the unique technical name used to derive the function name exposed to the AI model.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the natural-language description the AI model uses to tell instances apart.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is being created.
    /// </summary>
    public bool IsNew { get; set; }
}
