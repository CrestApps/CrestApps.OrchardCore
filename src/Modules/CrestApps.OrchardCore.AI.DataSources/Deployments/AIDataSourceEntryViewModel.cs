namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

/// <summary>
/// Represents the view model for AI data source entry.
/// </summary>
public class AIDataSourceEntryViewModel
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
