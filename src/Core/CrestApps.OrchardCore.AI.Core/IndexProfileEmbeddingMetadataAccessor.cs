using CrestApps.Core;
using CrestApps.Core.AI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides methods for reading and writing consolidated embedding metadata on an <see cref="IndexProfile"/>.
/// Merges legacy per-feature metadata keys into a single canonical entry.
/// </summary>
public static class IndexProfileEmbeddingMetadataAccessor
{
    private const string ChatInteractionMetadataKey = "ChatInteractionIndexProfileMetadata";
    private const string AIMemoryMetadataKey = "AIMemoryIndexProfileMetadata";

    /// <summary>
    /// Retrieves the consolidated <see cref="DataSourceIndexProfileMetadata"/> from the given index profile,
    /// merging any legacy chat-interaction and AI-memory metadata entries.
    /// </summary>
    /// <param name="indexProfile">The index profile to read metadata from.</param>
    public static DataSourceIndexProfileMetadata GetMetadata(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        if (!indexProfile.TryGet(out DataSourceIndexProfileMetadata metadata))
        {
            metadata = new DataSourceIndexProfileMetadata();
        }

        if (indexProfile.TryGet<DataSourceIndexProfileMetadata>(ChatInteractionMetadataKey, out var chatInteractionMetadata))
        {
            Merge(metadata, chatInteractionMetadata);
        }

        if (indexProfile.TryGet<DataSourceIndexProfileMetadata>(AIMemoryMetadataKey, out var aiMemoryMetadata))
        {
            Merge(metadata, aiMemoryMetadata);
        }

        return metadata;
    }

    /// <summary>
    /// Stores the given <see cref="DataSourceIndexProfileMetadata"/> on the index profile
    /// and removes any legacy per-feature metadata keys.
    /// </summary>
    /// <param name="indexProfile">The index profile to store metadata on.</param>
    /// <param name="metadata">The embedding metadata to persist.</param>
    public static void StoreMetadata(IndexProfile indexProfile, DataSourceIndexProfileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(metadata);

        indexProfile.Put(metadata);
        indexProfile.Remove(ChatInteractionMetadataKey);
        indexProfile.Remove(AIMemoryMetadataKey);
    }

    private static void Merge(DataSourceIndexProfileMetadata target, DataSourceIndexProfileMetadata source)
    {
        if (source == null)
        {
            return;
        }

        var sourceEmbeddingDeploymentName = source.GetEmbeddingDeploymentName();

        if (string.IsNullOrWhiteSpace(target.GetEmbeddingDeploymentName()) && !string.IsNullOrWhiteSpace(sourceEmbeddingDeploymentName))
        {
            target.SetEmbeddingDeploymentName(sourceEmbeddingDeploymentName);
        }
    }
}
