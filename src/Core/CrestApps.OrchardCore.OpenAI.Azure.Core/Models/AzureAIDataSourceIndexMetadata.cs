namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

/// <summary>
/// Represents index-level configuration metadata for Azure AI data sources.
/// This metadata is stored on the AIDataSource and contains only index-related settings.
/// Query-time parameters should use <see cref="AzureRagChatMetadata"/> on the AIProfile instead.
/// </summary>
public sealed class AzureAIDataSourceIndexMetadata
{
    /// <summary>
    /// Gets or sets the name of the index to use as a data source.
    /// </summary>
    public string IndexName { get; set; }
}
