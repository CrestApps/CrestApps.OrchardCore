using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    /// <summary>
    /// Updates existing tenants so legacy per-feature index profile metadata is consolidated into canonical metadata.
    /// </summary>
    public int UpdateFrom1()
    {
        if (_shellSettings.IsInitializing())
        {
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    /// <summary>
    /// Updates existing tenants so canonical embedding deployment identifiers are rewritten to deployment names.
    /// </summary>
    public int UpdateFrom2()
    {
        if (_shellSettings.IsInitializing())
        {
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    /// <summary>
    /// Updates existing tenants so stored embedding deployment selectors are normalized to deployment names.
    /// </summary>
    public int UpdateFrom3()
    {
        if (_shellSettings.IsInitializing())
        {
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    /// <summary>
    /// Updates existing tenants so legacy Azure provider aliases and prefixed selectors are normalized correctly.
    /// </summary>
    public int UpdateFrom4()
    {
        if (_shellSettings.IsInitializing())
        {
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    /// <summary>
    /// Updates existing tenants so index profiles are retried after deployment imports complete on earlier startups.
    /// </summary>
    public int UpdateFrom5()
    {
        if (_shellSettings.IsInitializing())
        {
            return 6;
        }

        ScheduleMigration();

        return 6;
    }

    private static void ScheduleMigration()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();
            var deploymentManager = scope.ServiceProvider.GetRequiredService<IAIDeploymentManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IndexProfileEmbeddingMetadataMigrations>>();

            foreach (var indexProfile in await indexProfileStore.GetAllAsync())
            {
                if (!await NormalizeIndexProfileMetadataAsync(indexProfile, deploymentManager, logger))
                {
                    continue;
                }

                await indexProfileStore.UpdateAsync(indexProfile);
            }
        });
    }

    private static async Task<bool> NormalizeIndexProfileMetadataAsync(
        IndexProfile indexProfile,
        IAIDeploymentManager deploymentManager,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(deploymentManager);
        ArgumentNullException.ThrowIfNull(logger);

        var hadLegacyMetadata = HasLegacyMetadata(indexProfile);

        var metadata = IndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);

        var canonicalEmbeddingDeploymentName = await ResolveCanonicalEmbeddingDeploymentNameAsync(indexProfile, deploymentManager, logger);

        if (!string.IsNullOrWhiteSpace(canonicalEmbeddingDeploymentName) &&
            !string.Equals(metadata.GetEmbeddingDeploymentName(), canonicalEmbeddingDeploymentName, StringComparison.OrdinalIgnoreCase))
        {
            metadata.SetEmbeddingDeploymentName(canonicalEmbeddingDeploymentName);
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

    private static async Task<string> ResolveCanonicalEmbeddingDeploymentNameAsync(
        IndexProfile indexProfile,
        IAIDeploymentManager deploymentManager,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(deploymentManager);
        ArgumentNullException.ThrowIfNull(logger);

        var legacyMetadata = GetLegacyMetadata(indexProfile, logger);

        if (legacyMetadata == null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Index profile '{IndexProfileName}' does not contain any legacy embedding metadata to normalize.",
                    indexProfile.Name);
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingDeploymentId))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Resolving embedding deployment by legacy id '{EmbeddingDeploymentId}' for index profile '{IndexProfileName}'.",
                    legacyMetadata.EmbeddingDeploymentId,
                    indexProfile.Name);
            }

            var deploymentById = await deploymentManager.FindByIdAsync(legacyMetadata.EmbeddingDeploymentId);

            if (deploymentById?.SupportsType(AIDeploymentType.Embedding) == true)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Resolved legacy embedding deployment id '{EmbeddingDeploymentId}' to deployment name '{DeploymentName}' for index profile '{IndexProfileName}'.",
                        legacyMetadata.EmbeddingDeploymentId,
                        deploymentById.Name,
                        indexProfile.Name);
                }

                return deploymentById.Name;
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding deployment id '{EmbeddingDeploymentId}' did not resolve to a valid embedding deployment for index profile '{IndexProfileName}'. Resolved deployment name: '{ResolvedDeploymentName}', type: '{ResolvedDeploymentType}'.",
                    legacyMetadata.EmbeddingDeploymentId,
                    indexProfile.Name,
                    deploymentById?.Name,
                    deploymentById?.Type);
            }
        }

        if (string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingDeploymentName))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Index profile '{IndexProfileName}' has no legacy embedding deployment name after metadata extraction.",
                    indexProfile.Name);
            }

            return null;
        }

        var deployment = await deploymentManager.FindByNameAsync(legacyMetadata.EmbeddingDeploymentName);

        if (deployment?.SupportsType(AIDeploymentType.Embedding) == true &&
            MatchesLegacyDeploymentSelector(deployment, legacyMetadata, logger))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Resolved legacy embedding selector '{EmbeddingDeploymentName}' directly to deployment name '{DeploymentName}' for index profile '{IndexProfileName}'.",
                    legacyMetadata.EmbeddingDeploymentName,
                    deployment.Name,
                    indexProfile.Name);
            }

            return deployment.Name;
        }

        var deployments = ((await deploymentManager.GetByTypeAsync(AIDeploymentType.Embedding)) ?? [])
            .Where(candidate => MatchesLegacyDeploymentSelector(candidate, legacyMetadata, logger))
            .ToArray();

        if (deployments.Length == 1)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Resolved legacy embedding selector '{EmbeddingDeploymentName}' to unique embedding deployment '{DeploymentName}' from all embedding deployments for index profile '{IndexProfileName}'.",
                    legacyMetadata.EmbeddingDeploymentName,
                    deployments[0].Name,
                    indexProfile.Name);
            }

            return deployments[0].Name;
        }

        if (string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingProviderName) ||
            string.IsNullOrWhiteSpace(legacyMetadata.EmbeddingConnectionName))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding selector '{EmbeddingDeploymentName}' for index profile '{IndexProfileName}' has no provider/connection scope, so normalization is leaving the existing selector unchanged.",
                    legacyMetadata.EmbeddingDeploymentName,
                    indexProfile.Name);
            }

            return legacyMetadata.EmbeddingDeploymentName;
        }

        deployments = await GetProviderScopedMatchesAsync(deploymentManager, legacyMetadata, logger);

        if (deployments.Length != 1)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding selector '{EmbeddingDeploymentName}' for index profile '{IndexProfileName}' resolved to {MatchCount} deployments in provider '{ProviderName}' and connection '{ConnectionName}'. Provider aliases considered: '{ProviderAliases}'.",
                    legacyMetadata.EmbeddingDeploymentName,
                    indexProfile.Name,
                    deployments.Length,
                    legacyMetadata.EmbeddingProviderName,
                    legacyMetadata.EmbeddingConnectionName,
                    string.Join(", ", GetProviderAliases(legacyMetadata.EmbeddingProviderName)));
            }
        }

        return deployments.Length == 1
            ? deployments[0].Name
            : null;
    }

    private static async Task<AIDeployment[]> GetProviderScopedMatchesAsync(
        IAIDeploymentManager deploymentManager,
        LegacyIndexProfileEmbeddingMetadata metadata,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(deploymentManager);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(logger);

        var providerAliases = GetProviderAliases(metadata.EmbeddingProviderName);
        var deploymentMap = new Dictionary<string, AIDeployment>(StringComparer.OrdinalIgnoreCase);

        foreach (var providerAlias in providerAliases)
        {
            var scopedDeployments = await deploymentManager.GetAllAsync(providerAlias) ?? [];

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Queried {DeploymentCount} deployments for legacy embedding provider alias '{ProviderAlias}'.",
                    scopedDeployments.Count(),
                    providerAlias);
            }

            foreach (var deployment in scopedDeployments)
            {
                deploymentMap.TryAdd(deployment.ItemId ?? deployment.Name ?? Guid.NewGuid().ToString("N"), deployment);
            }
        }

        if (deploymentMap.Count == 0)
        {
            var allDeployments = await deploymentManager.GetAllAsync() ?? [];

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "No deployments were returned for provider aliases '{ProviderAliases}'. Falling back to all {DeploymentCount} deployments for selector matching.",
                    string.Join(", ", providerAliases),
                    allDeployments.Count());
            }

            foreach (var deployment in allDeployments)
            {
                deploymentMap.TryAdd(deployment.ItemId ?? deployment.Name ?? Guid.NewGuid().ToString("N"), deployment);
            }
        }

        return deploymentMap.Values
            .Where(candidate => MatchesLegacyDeploymentSelector(candidate, metadata, logger))
            .ToArray();
    }

    private static LegacyIndexProfileEmbeddingMetadata GetLegacyMetadata(IndexProfile indexProfile, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(logger);

        var metadata = new LegacyIndexProfileEmbeddingMetadata();

        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, nameof(DataSourceIndexProfileMetadata), logger));
        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, "ChatInteractionIndexProfileMetadata", logger));
        MergeLegacyMetadata(metadata, TryGetLegacyMetadata(indexProfile, "AIMemoryIndexProfileMetadata", logger));

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Merged legacy embedding metadata for index profile '{IndexProfileName}'. DeploymentId: '{EmbeddingDeploymentId}', ProviderName: '{EmbeddingProviderName}', ConnectionName: '{EmbeddingConnectionName}', DeploymentName: '{EmbeddingDeploymentName}'.",
                indexProfile.Name,
                metadata.EmbeddingDeploymentId,
                metadata.EmbeddingProviderName,
                metadata.EmbeddingConnectionName,
                metadata.EmbeddingDeploymentName);
        }

        return string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentId) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingProviderName) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingConnectionName) &&
            string.IsNullOrWhiteSpace(metadata.EmbeddingDeploymentName)
            ? null
            : metadata;
    }

    private static LegacyIndexProfileEmbeddingMetadata TryGetLegacyMetadata(IndexProfile indexProfile, string key, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(logger);

        if (!indexProfile.TryGet<LegacyIndexProfileEmbeddingMetadata>(key, out var metadata))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Index profile '{IndexProfileName}' does not contain legacy embedding metadata under key '{MetadataKey}'.",
                    indexProfile.Name,
                    key);
            }

            return null;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Read legacy embedding metadata from key '{MetadataKey}' for index profile '{IndexProfileName}'. DeploymentId: '{EmbeddingDeploymentId}', ProviderName: '{EmbeddingProviderName}', ConnectionName: '{EmbeddingConnectionName}', DeploymentName: '{EmbeddingDeploymentName}'.",
                key,
                indexProfile.Name,
                metadata.EmbeddingDeploymentId,
                metadata.EmbeddingProviderName,
                metadata.EmbeddingConnectionName,
                metadata.EmbeddingDeploymentName);
        }

        return metadata;
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

    private static bool MatchesLegacyDeploymentSelector(
        AIDeployment deployment,
        LegacyIndexProfileEmbeddingMetadata metadata,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (deployment == null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Legacy embedding selector comparison failed because the deployment candidate is null.");
            }

            return false;
        }

        if (!deployment.SupportsType(AIDeploymentType.Embedding))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding selector comparison failed for deployment '{DeploymentName}' because its type '{DeploymentType}' does not support embeddings.",
                    deployment.Name,
                    deployment.Type);
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.EmbeddingProviderName) &&
            !GetProviderAliases(metadata.EmbeddingProviderName).Contains(deployment.ClientName, StringComparer.OrdinalIgnoreCase))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding selector comparison failed for deployment '{DeploymentName}' because provider '{ActualProviderName}' did not match expected provider '{ExpectedProviderName}'.",
                    deployment.Name,
                    deployment.ClientName,
                    metadata.EmbeddingProviderName);
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.EmbeddingConnectionName) &&
            !string.Equals(deployment.ConnectionName, metadata.EmbeddingConnectionName, StringComparison.OrdinalIgnoreCase))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Legacy embedding selector comparison failed for deployment '{DeploymentName}' because connection '{ActualConnectionName}' did not match expected connection '{ExpectedConnectionName}'.",
                    deployment.Name,
                    deployment.ConnectionName,
                    metadata.EmbeddingConnectionName);
            }

            return false;
        }

        var selectorCandidates = GetDeploymentSelectorCandidates(metadata.EmbeddingDeploymentName);
        var matchesByName = selectorCandidates.Contains(deployment.Name, StringComparer.OrdinalIgnoreCase);
        var matchesByModelName = selectorCandidates.Contains(deployment.ModelName, StringComparer.OrdinalIgnoreCase);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Legacy embedding selector final comparison for deployment '{DeploymentName}'. Expected selector: '{ExpectedDeploymentSelector}', Candidate selectors: '{SelectorCandidates}', Actual deployment name: '{ActualDeploymentName}', Actual model name: '{ActualModelName}', MatchesByName: {MatchesByName}, MatchesByModelName: {MatchesByModelName}.",
                deployment.Name,
                metadata.EmbeddingDeploymentName,
                string.Join(", ", selectorCandidates),
                deployment.Name,
                deployment.ModelName,
                matchesByName,
                matchesByModelName);
        }

        return matchesByName || matchesByModelName;
    }

    private static string[] GetProviderAliases(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return [];
        }

        var normalizedProviderName = providerName.Trim();

        if (string.Equals(normalizedProviderName, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            return ["Azure", "AzureOpenAI", "AzureOpenAIOwnData"];
        }

        if (string.Equals(normalizedProviderName, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return ["AzureOpenAI", "Azure", "AzureOpenAIOwnData"];
        }

        if (string.Equals(normalizedProviderName, "AzureOpenAIOwnData", StringComparison.OrdinalIgnoreCase))
        {
            return ["AzureOpenAIOwnData", "AzureOpenAI", "Azure"];
        }

        return [normalizedProviderName];
    }

    private static string[] GetDeploymentSelectorCandidates(string deploymentSelector)
    {
        if (string.IsNullOrWhiteSpace(deploymentSelector))
        {
            return [];
        }

        var selector = deploymentSelector.Trim();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            selector,
        };

        foreach (var providerAlias in GetProviderAliases("Azure"))
        {
            var prefix = providerAlias + "-";

            if (selector.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(selector.Substring(prefix.Length));
            }
        }

        return [.. candidates];
    }

    private sealed class LegacyIndexProfileEmbeddingMetadata
    {
        public string EmbeddingDeploymentId { get; set; }

        public string EmbeddingProviderName { get; set; }

        public string EmbeddingConnectionName { get; set; }

        public string EmbeddingDeploymentName { get; set; }
    }
}
