namespace CrestApps.OrchardCore.AI.Chat.Interactions;

/// <summary>
/// Metadata for storing data source configuration on a ChatInteraction.
/// Used when the interaction uses AzureOpenAIOwnData provider.
/// </summary>
public class ChatInteractionDataSourceMetadata
{
    /// <summary>
    /// Gets or sets the data source type (e.g., "azure_search", "elasticsearch", "mongo_db").
    /// </summary>
    public string DataSourceType { get; set; }

    /// <summary>
    /// Gets or sets the data source ID.
    /// </summary>
    public string DataSourceId { get; set; }
}
