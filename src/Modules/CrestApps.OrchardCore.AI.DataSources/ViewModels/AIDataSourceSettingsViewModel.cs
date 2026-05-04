namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// Represents the view model for AI data source settings.
/// </summary>
public class AIDataSourceSettingsViewModel
{
    /// <summary>
    /// Gets or sets the default strictness.
    /// </summary>
    public int DefaultStrictness { get; set; }

    /// <summary>
    /// Gets or sets the default top n documents.
    /// </summary>
    public int DefaultTopNDocuments { get; set; }
}
