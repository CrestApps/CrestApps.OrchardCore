namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents an AI tool instance that can be made available during post-session processing.
/// </summary>
public class PostSessionToolInstanceEntry
{
    /// <summary>
    /// Gets or sets the unique instance name used as the stable reference stored on the profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the instance description shown to the model.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
