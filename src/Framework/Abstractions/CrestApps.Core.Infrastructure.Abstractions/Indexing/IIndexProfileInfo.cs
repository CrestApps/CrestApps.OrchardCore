namespace CrestApps.Core.Infrastructure.Indexing;

/// <summary>
/// Provides index profile information for data source and vector search operations.
/// This abstraction decouples the AI framework from specific indexing implementations.
/// </summary>
public interface IIndexProfileInfo
{
    /// <summary>
    /// Gets the unique identifier for the index profile.
    /// </summary>
    string IndexProfileId { get; }

    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    string IndexName { get; }

    /// <summary>
    /// Gets the name of the index provider (e.g., "Elasticsearch", "AzureAISearch").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the full name of the index including any tenant prefix.
    /// </summary>
    string IndexFullName { get; }
}
