using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class IndexProfileEmbeddingMetadataMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public IndexProfileEmbeddingMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 1;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();

            foreach (var indexProfile in await indexProfileStore.GetAllAsync())
            {
                var hadLegacyMetadata =
                    indexProfile.Has("ChatInteractionIndexProfileMetadata") ||
                    indexProfile.Has("AIMemoryIndexProfileMetadata");

                var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

                if (string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentId))
                {
                    var deployment = await EmbeddingDeploymentResolver.FindEmbeddingDeploymentAsync(deploymentManager, metadata);

                    if (deployment != null)
                    {
                        metadata.EmbeddingDeploymentId = deployment.ItemId;
                    }
                }

                if (!hadLegacyMetadata && string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentId))
                {
                    continue;
                }

                IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata);
                await indexProfileStore.UpdateAsync(indexProfile);
            }
        });

        return 1;
    }
}
