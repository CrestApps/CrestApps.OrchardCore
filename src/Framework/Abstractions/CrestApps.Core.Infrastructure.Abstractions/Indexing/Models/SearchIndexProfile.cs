using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.Infrastructure.Indexing.Models;

/// <summary>
/// Represents an index profile that defines how a search index is configured
/// and which provider manages it (e.g., Elasticsearch, Azure AI Search).
/// </summary>
public sealed class SearchIndexProfile : CatalogItem, IIndexProfileInfo, INameAwareModel, IDisplayTextAwareModel, ICloneable<SearchIndexProfile>
{
    /// <summary>
    /// Gets or sets the unique name for this index profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text for this index profile.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name of the underlying search index.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the search provider name (e.g., "Elasticsearch", "AzureAISearch").
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified index name, which may include a tenant or application prefix.
    /// </summary>
    public string IndexFullName { get; set; }

    /// <summary>
    /// Gets or sets the type of data stored in this index (e.g., "AIDocuments", "DataSourceIndex", "AIMemory").
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the deployment ID used for generating embeddings.
    /// </summary>
    public string EmbeddingDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the date and time when this index profile was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public string Author { get; set; }

    string IIndexProfileInfo.IndexProfileId => ItemId;

    public SearchIndexProfile Clone()
    {
        return new SearchIndexProfile
        {
            ItemId = ItemId,
            Name = Name,
            DisplayText = DisplayText,
            IndexName = IndexName,
            ProviderName = ProviderName,
            IndexFullName = IndexFullName,
            Type = Type,
            EmbeddingDeploymentId = EmbeddingDeploymentId,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = Properties.Clone(),
        };
    }
}
