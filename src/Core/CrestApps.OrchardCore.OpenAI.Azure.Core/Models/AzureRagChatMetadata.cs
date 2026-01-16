namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

/// <summary>
/// Represents query-time RAG (Retrieval-Augmented Generation) parameters.
/// This metadata can be attached to AIProfile or ChatInteraction to customize
/// RAG behavior without requiring new data sources.
/// </summary>
public sealed class AzureRagChatMetadata
{
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
    /// Gets or sets the OData filter expression to query a subset of indexed data.
    /// Example: "category eq 'documentation' or status ne 'archived'".
    /// </summary>
    public string Filter { get; set; }
}
