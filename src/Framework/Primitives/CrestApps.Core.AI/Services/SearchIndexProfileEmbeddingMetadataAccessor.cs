using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.AI.Services;

internal static class SearchIndexProfileEmbeddingMetadataAccessor
{
    private const string ChatInteractionMetadataKey = "ChatInteractionIndexProfileMetadata";
    private const string AIMemoryMetadataKey = "AIMemoryIndexProfileMetadata";

    public static DataSourceIndexProfileMetadata GetMetadata(SearchIndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        var metadata = indexProfile.As<DataSourceIndexProfileMetadata>();
        metadata.EmbeddingDeploymentId ??= indexProfile.EmbeddingDeploymentId;

        Merge(metadata, indexProfile.Get<DataSourceIndexProfileMetadata>(ChatInteractionMetadataKey));
        Merge(metadata, indexProfile.Get<DataSourceIndexProfileMetadata>(AIMemoryMetadataKey));

        return metadata;
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
