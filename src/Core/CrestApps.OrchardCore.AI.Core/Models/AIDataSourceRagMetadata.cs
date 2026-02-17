namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Represents query-time RAG (Retrieval-Augmented Generation) parameters for data sources.
/// This metadata can be attached to AIProfile or ChatInteraction to customize
/// RAG behavior for any provider.
/// </summary>
public sealed class AIDataSourceRagMetadata
{
    /// <summary>
    /// Gets or sets whether Early RAG is enabled for this profile.
    /// When null, the global site setting is used.
    /// </summary>
    public bool EnableEarlyRag { get; set; }

    /// <summary>
    /// Gets or sets the strictness threshold for categorizing documents as relevant.
    /// Values range from 1 to 5, with higher values meaning a higher threshold for relevance.
    /// </summary>
    public int? Strictness { get; set; }

    /// <summary>
    /// Gets or sets the number of top-scoring documents to retrieve from the data index.
    /// Values range from 3 to 20.
    /// </summary>
    public int? TopNDocuments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to limit retrieval to in-scope documents only.
    /// </summary>
    public bool IsInScope { get; set; } = true;

    /// <summary>
    /// Gets or sets the filter expression to query a subset of indexed data.
    /// The filter format depends on the search provider (e.g., Elasticsearch query for Elasticsearch).
    /// </summary>
    public string Filter { get; set; }
}
