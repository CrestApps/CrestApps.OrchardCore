namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Represents index-level configuration metadata for AI data sources.
/// This metadata is stored on the AIDataSource and contains only index-related settings.
/// </summary>
public sealed class AIDataSourceIndexMetadata
{
    /// <summary>
    /// Gets or sets the name of the source index to query for data.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the name of the master embedding index used to store document embeddings.
    /// </summary>
    public string MasterIndexName { get; set; }

    /// <summary>
    /// Gets or sets the source index field name that maps to the document title.
    /// </summary>
    public string TitleFieldName { get; set; }

    /// <summary>
    /// Gets or sets the source index field name that maps to the document content (text).
    /// </summary>
    public string ContentFieldName { get; set; }
}
