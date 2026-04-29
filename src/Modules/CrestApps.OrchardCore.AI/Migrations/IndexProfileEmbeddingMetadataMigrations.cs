using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Migrations;

internal sealed class IndexProfileEmbeddingMetadataMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexProfileEmbeddingMetadataMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    public IndexProfileEmbeddingMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Creates the initial migration state and schedules index profile metadata normalization for existing records.
    /// </summary>
    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        ScheduleMigration();

        return 3;
    }

    /// <summary>
    /// Updates existing tenants so legacy per-feature index profile metadata is consolidated into canonical metadata.
    /// </summary>
    public int UpdateFrom1()
    {
        if (_shellSettings.IsInitializing())
        {
            return 2;
        }

        ScheduleMigration();

        return 2;
    }

    /// <summary>
    /// Updates existing tenants so canonical embedding deployment identifiers are rewritten to deployment names.
    /// </summary>
    public int UpdateFrom2()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        ScheduleMigration();

        return 3;
    }

    private static void ScheduleMigration()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();

            foreach (var indexProfile in await indexProfileStore.GetAllAsync())
            {
                if (!await NormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager))
                {
                    continue;
                }

                await indexProfileStore.UpdateAsync(indexProfile);
            }
        });
    }

    private static async Task<bool> NormalizeIndexProfileMetadataAsync(IndexProfile indexProfile, IAIDeploymentManager deploymentManager)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var hadLegacyMetadata = HasLegacyMetadata(indexProfile);

        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        if (string.IsNullOrWhiteSpace(metadata.GetEmbeddingDeploymentName()))
        {
            metadata.SetEmbeddingDeploymentName(await ResolveCanonicalEmbeddingDeploymentNameAsync(indexProfile, deploymentManager));
        }

        if (!hadLegacyMetadata && string.IsNullOrWhiteSpace(metadata.GetEmbeddingDeploymentName()))
        {
            return false;
        }

        IndexProfileEmbeddingMetadataAccessor.StoreMetadata(indexProfile, metadata);

        return true;
    }

    private static bool HasLegacyMetadata(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        return indexProfile.Has(nameof(DataSourceIndexProfileMetadata)) ||
            indexProfile.Has("ChatInteractionIndexProfileMetadata") ||
            indexProfile.Has("AIMemoryIndexProfileMetadata");
    }

    private static async Task<string> ResolveCanonicalEmbeddingDeploymentNameAsync(IndexProfile indexProfile, IAIDeploymentManager deploymentManager)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(deploymentManager);

        var legacyMetadata = GetLegacyMetadata(indexProfile);

        if (legacyMetadata == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingDeploymentId))
        {
            var deploymentById = await deploymentManager.FindByIdAsync(legacyMetadata.EmbeddingDeploymentId);

            if (deploymentById?.SupportsType(AIDeploymentType.Embedding) == true)
            {
                return deploymentById.Name;
            }
        }

        if (string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingDeploymentName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingProviderName) ||
            string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingConnectionName))
        {
            return legacyMetadata.EmbeddingDeploymentName;
        }

        var deployment = await deploymentManager.FindByNameAsync(legacyMetadata.EmbeddingDeploymentName);

        if (IsMatchingLegacyEmbeddingDeployment(deployment, legacyMetadata))
        {
            return deployment.Name;
        }

        var deployments = await deploymentManager.GetAllAsync(legacyMetadata.EmbeddingProviderName);

        return deployments
            .FirstOrDefault(candidate => IsMatchingLegacyEmbeddingDeployment(candidate, legacyMetadata))
            ?.Name;
    }

    private static LegacyIndexProfileEmbeddingMetadata GetLegacyMetadata(IndexProfile indexProfile)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        var metadata = new LegacyIndexProfileEmbeddingMetadata();

        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, nameof(DataSourceIndexProfileMetadata)));
        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, "ChatInteractionIndexProfileMetadata"));
        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, "AIMemoryIndexProfileMetadata"));

        return string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentId) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingProviderName) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingConnectionName) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentName)
            ? null
            : metadata;
    }

    private static LegacyIndexProfileEmbeddingMetadata TryGetLegacyMetadata(IndexProfile indexProfile, string key)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return indexProfile.TryGet<LegacyIndexProfileEmbeddingMetadata>(key, out var metadata)
            ? metadata
            : null;
    }

    private static void MergeLegacyMetadata(LegacyIndexProfileEmbeddingMetadata target, LegacyIndexProfileEmbeddingMetadata source)
    {
        if (source == null)
        {
            return;
        }

        target.EmbeddingDeploymentId ??= source.EmbeddingDeploymentId;
        target.EmbeddingProviderName ??= source.EmbeddingProviderName;
        target.EmbeddingConnectionName ??= source.EmbeddingConnectionName;
        target.EmbeddingDeploymentName ??= source.EmbeddingDeploymentName;
    }

    private static bool IsMatchingLegacyEmbeddingDeployment(AIDeployment deployment, LegacyIndexProfileEmbeddingMetadata metadata)
    {
        if (deployment == null || !deployment.SupportsType(AIDeploymentType.Embedding))
        {
            return false;
        }

        return string.Equals(deployment.ClientName, metadata.EmbeddingProviderName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(deployment.ConnectionName, metadata.EmbeddingConnectionName, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(deployment.Name, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(deployment.ModelName, metadata.EmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class LegacyIndexProfileEmbeddingMetadata
    {
        public string EmbeddingDeploymentId { get; set; }

        public string EmbeddingProviderName { get; set; }

        public string EmbeddingConnectionName { get; set; }

        public string EmbeddingDeploymentName { get; set; }
    }
}
