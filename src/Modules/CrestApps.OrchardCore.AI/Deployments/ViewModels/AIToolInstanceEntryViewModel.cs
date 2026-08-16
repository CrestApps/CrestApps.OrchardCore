namespace CrestApps.OrchardCore.AI.Deployments.ViewModels;

/// <summary>
/// Represents the view model for an AI tool instance entry.
/// </summary>
public class AIToolInstanceEntryViewModel
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
