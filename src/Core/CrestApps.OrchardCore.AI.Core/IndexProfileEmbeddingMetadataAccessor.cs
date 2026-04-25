using CrestApps.Core;
using CrestApps.Core.AI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

public static class IndexProfileEmbeddingMetadataAccessor
{
    private const string ChatInteractionMetadataKey = "ChatInteractionIndexProfileMetadata";
    private const string AIMemoryMetadataKey = "AIMemoryIndexProfileMetadata";

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

    public static void StoreMetadata(IndexProfile indexProfile, DataSourceIndexProfileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(metadata);

#pragma warning disable CS0618 // Type or member is obsolete
        metadata.EmbeddingProviderName = null;
        metadata.EmbeddingConnectionName = null;
        metadata.EmbeddingDeploymentName = null;
#pragma warning restore CS0618 // Type or member is obsolete

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

        target.EmbeddingDeploymentId ??= source.EmbeddingDeploymentId;

#pragma warning disable CS0618 // Type or member is obsolete
        target.EmbeddingProviderName ??= source.EmbeddingProviderName;
        target.EmbeddingConnectionName ??= source.EmbeddingConnectionName;
        target.EmbeddingDeploymentName ??= source.EmbeddingDeploymentName;
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
