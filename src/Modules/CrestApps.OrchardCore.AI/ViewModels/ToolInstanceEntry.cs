namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents a selectable AI tool instance entry.
/// </summary>
public class ToolInstanceEntry
{
    /// <summary>
    /// Gets or sets the unique technical name of the tool instance.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description presented to the AI model.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool instance is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
