using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

public static class EmbeddingDeploymentNameAccessor
{
    public static string GetEmbeddingDeploymentName(this DataSourceIndexProfileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.EmbeddingDeploymentName;
    }

    public static void SetEmbeddingDeploymentName(this DataSourceIndexProfileMetadata metadata, string value)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        metadata.EmbeddingDeploymentName = value;
    }

    public static string GetEmbeddingDeploymentName(this SearchIndexProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.EmbeddingDeploymentName;
    }

    public static void SetEmbeddingDeploymentName(this SearchIndexProfile profile, string value)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile.EmbeddingDeploymentName = value;
    }
}
