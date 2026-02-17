using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

/// <summary>
/// Migrates existing Azure AI data source metadata from the legacy format to the new format.
/// This migration:
/// 1. Extracts IndexName from legacy metadata and stores it as a first-class property on AIDataSource
/// 2. Extracts query-time parameters (Filter, Strictness, TopNDocuments) and stores them in AIDataSourceRagMetadata on AI profiles
/// </summary>
internal sealed class AzureOpenAIDataSourceMetadataMigrations : DataMigration
{
    // Legacy metadata class names (used for Properties dictionary access)
    private const string LegacyAISearchMetadataName = "AzureAIProfileAISearchMetadata";
    private const string LegacyElasticsearchMetadataName = "AzureAIProfileElasticsearchMetadata";
    private const string LegacyMongoDBMetadataName = "AzureAIProfileMongoDBMetadata";

    private readonly ShellSettings _shellSettings;
    private readonly ICatalog<AIDataSource> _sataSourceStore;
    private readonly IIndexProfileManager _indexProfileManager;
    private readonly IIndexProfileStore _indexProfileStore;

    public AzureOpenAIDataSourceMetadataMigrations(
        ShellSettings shellSettings,
        ICatalog<AIDataSource> sataSourceStore,
        IIndexProfileManager indexProfileManager,
        IIndexProfileStore indexProfileStore)
    {
        _shellSettings = shellSettings;
        _sataSourceStore = sataSourceStore;
        _indexProfileManager = indexProfileManager;
        _indexProfileStore = indexProfileStore;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();
            var profileStore = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIProfile>>();

            // Migrate data sources to use first-class index properties
            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                // Use legacy ProfileSource or ProviderName from Properties (JsonExtensionData)
                var profileSource = dataSource.Properties?["ProfileSource"]?.GetValue<string>();
                var providerName = dataSource.Properties?["ProviderName"]?.GetValue<string>();

                if (!string.Equals(profileSource, AzureOpenAIConstants.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(profileSource, "AzureOpenAIOwnData", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var needsUpdate = false;

                // Skip if IndexName is already set as a first-class property.
                if (!string.IsNullOrWhiteSpace(dataSource.SourceIndexProfileName))
                {
                    continue;
                }

                var legacyType = dataSource.Properties?["Type"]?.GetValue<string>();

                // Extract IndexName based on legacy data source type using Properties dictionary
                switch (legacyType)
                {
                    case "azure_search":
                        {
                            var indexName = GetPropertyValue<string>(dataSource.Properties, LegacyAISearchMetadataName, "IndexName");
                            if (!string.IsNullOrWhiteSpace(indexName))
                            {
                                dataSource.SourceIndexProfileName = indexName;
                                needsUpdate = true;
                            }
                        }
                        break;

                    case "elasticsearch":
                        {
                            var indexName = GetPropertyValue<string>(dataSource.Properties, LegacyElasticsearchMetadataName, "IndexName");
                            if (!string.IsNullOrWhiteSpace(indexName))
                            {
                                dataSource.SourceIndexProfileName = indexName;
                                needsUpdate = true;
                            }
                        }
                        break;
                }

                if (needsUpdate)
                {
                    await dataSourceStore.UpdateAsync(dataSource);
                }
            }

            // Migrate AI profiles to use the new RAG metadata
            foreach (var profile in await profileStore.GetAllAsync())
            {
                if (profile.Source != AzureOpenAIConstants.ProviderName && profile.Source != "AzureOpenAIOwnData")
                {
                    continue;
                }

                var dataSourceMetadata = profile.As<AIProfileDataSourceMetadata>();
                if (string.IsNullOrEmpty(dataSourceMetadata?.DataSourceId))
                {
                    continue;
                }

                // Check if new RAG metadata already exists
                if (profile.TryGet<AIDataSourceRagMetadata>(out var existingRagMetadata) &&
                    (existingRagMetadata.Strictness.HasValue ||
                     existingRagMetadata.TopNDocuments.HasValue ||
                     !string.IsNullOrWhiteSpace(existingRagMetadata.Filter)))
                {
                    // Already migrated
                    continue;
                }

                // Find the associated data source
                var dataSource = await dataSourceStore.FindByIdAsync(dataSourceMetadata.DataSourceId);
                if (dataSource is null)
                {
                    continue;
                }

                int? strictness = null;
                int? topNDocuments = null;
                string filter = null;

                var legacyType = dataSource.Properties?["Type"]?.GetValue<string>();

                // Extract query parameters based on legacy data source type using Properties dictionary
                switch (legacyType)
                {
                    case "azure_search":
                        {
                            var legacyProps = GetPropertyObject(dataSource.Properties, LegacyAISearchMetadataName);
                            if (legacyProps != null)
                            {
                                strictness = GetNullableInt(legacyProps, "Strictness");
                                topNDocuments = GetNullableInt(legacyProps, "TopNDocuments");
                                filter = legacyProps["Filter"]?.GetValue<string>();
                            }
                        }
                        break;

                    case "elasticsearch":
                        {
                            var legacyProps = GetPropertyObject(dataSource.Properties, LegacyElasticsearchMetadataName);
                            if (legacyProps != null)
                            {
                                strictness = GetNullableInt(legacyProps, "Strictness");
                                topNDocuments = GetNullableInt(legacyProps, "TopNDocuments");
                            }
                        }
                        break;

                    case "mongo_db":
                        {
                            var legacyProps = GetPropertyObject(dataSource.Properties, LegacyMongoDBMetadataName);
                            if (legacyProps != null)
                            {
                                strictness = GetNullableInt(legacyProps, "Strictness");
                                topNDocuments = GetNullableInt(legacyProps, "TopNDocuments");
                            }
                        }
                        break;
                }

                // Store query parameters on the profile
                if (strictness.HasValue || topNDocuments.HasValue || !string.IsNullOrWhiteSpace(filter))
                {
                    profile.Put(new AIDataSourceRagMetadata
                    {
                        Strictness = strictness,
                        TopNDocuments = topNDocuments,
                        IsInScope = true,
                        Filter = filter,
                    });

                    await profileStore.UpdateAsync(profile);
                }
            }
        });

        return 3;
    }

    public async Task<int> UpdateFrom1Async()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }


        await MigrateKnowledgeBaseIndexesAsync();

        return 3;
    }

    public async Task<int> UpdateFrom2Async()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        await MigrateKnowledgeBaseIndexesAsync();

        return 3;
    }

    internal async Task MigrateKnowledgeBaseIndexesAsync()
    {
        var dataSources = await _sataSourceStore.GetAllAsync();

        // Create one AI KB index per provider (if needed) before updating data sources.
        var masterIndexByProvider = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataSource in dataSources)
        {
            var providerName = GetLegacyProviderName(dataSource);

            if (string.IsNullOrEmpty(providerName) || !IsSupportedProvider(providerName))
            {
                continue;
            }

            if (masterIndexByProvider.ContainsKey(providerName))
            {
                continue;
            }

            var masterIndexName = await EnsureKnowledgeBaseIndexAsync(providerName);

            if (!string.IsNullOrEmpty(masterIndexName))
            {
                masterIndexByProvider[providerName] = masterIndexName;
            }
        }

        // Migrate field mappings + master index linkage, then trigger indexing.
        foreach (var dataSource in dataSources)
        {
            var providerName = GetLegacyProviderName(dataSource);

            if (string.IsNullOrEmpty(providerName) || !IsSupportedProvider(providerName))
            {
                continue;
            }

            var needsUpdate = false;

            if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) &&
                masterIndexByProvider.TryGetValue(providerName, out var masterIndexName))
            {
                dataSource.AIKnowledgeBaseIndexProfileName = masterIndexName;
                needsUpdate = true;
            }

            // Set default field mappings based on the provider.
            if (string.IsNullOrEmpty(dataSource.ContentFieldName))
            {
                dataSource.KeyFieldName ??= "ContentItemId";

                if (string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase))
                {
                    dataSource.TitleFieldName ??= "Content.ContentItem.DisplayText.Analyzed";
                    dataSource.ContentFieldName ??= "Content.ContentItem.FullText";
                }
                else if (string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
                {
                    dataSource.TitleFieldName ??= "Content__ContentItem__DisplayText__Analyzed";
                    dataSource.ContentFieldName ??= "Content__ContentItem__FullText";
                }

                needsUpdate = true;
            }

            if (!needsUpdate)
            {
                continue;
            }

            await _sataSourceStore.UpdateAsync(dataSource);

            // Trigger indexing in the background (best-effort).
            await TrySyncDataSourceAsync(dataSource);
        }
    }

    private static bool IsSupportedProvider(string providerName)
        => string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase);

    private static string GetLegacyProviderName(AIDataSource dataSource)
    {
        var providerName = dataSource.Properties?["ProviderName"]?.GetValue<string>();

        if (!string.IsNullOrEmpty(providerName))
        {
            return providerName;
        }

        var profileSource = dataSource.Properties?["ProfileSource"]?.GetValue<string>();
        var type = dataSource.Properties?["Type"]?.GetValue<string>();

        if (!string.IsNullOrEmpty(profileSource))
        {
            return type switch
            {
                "elasticsearch" => "Elasticsearch",
                "azure_search" => "AzureAISearch",
                _ => type,
            };
        }

        return null;
    }

    private async Task<string> EnsureKnowledgeBaseIndexAsync(string providerName)
    {
        // Prefer an existing DataSource master index for this provider.
        var existing = await _indexProfileStore.GetAsync(providerName, DataSourceConstants.IndexingTaskType);

        if (!existing.Any())
        {
            var preferred = existing.FirstOrDefault(x => x.Name.StartsWith("AIKnowledgeBaseWarehouse", StringComparison.OrdinalIgnoreCase));

            return (preferred ?? existing.First()).Name;
        }

        var name = GetKnowledgeBaseWarehouseName(providerName);

        // Avoid duplicates if already present by name.
        if (await _indexProfileManager.FindByNameAsync(name) is { } byName)
        {
            return byName.Name;
        }

        var indexProfile = await _indexProfileManager.NewAsync(providerName, DataSourceConstants.IndexingTaskType);
        indexProfile.Name = name;

        var validationResult = await _indexProfileManager.ValidateAsync(indexProfile);

        if (!validationResult.Succeeded)
        {
            // log error.
            return null;
        }

        await _indexProfileManager.CreateAsync(indexProfile);

        return indexProfile.Name;
    }

    private static string GetKnowledgeBaseWarehouseName(string providerName)
        => string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase)
            ? "AIKnowledgeBaseWarehouse"
            : "AIKnowledgeBaseWarehouseAZ";

    private async Task TrySyncDataSourceAsync(AIDataSource dataSource)
    {
        // Do not take a compile-time dependency on CrestApps.OrchardCore.AI.DataSources.
        // If the module is enabled, call DataSourceIndexingService.SyncDataSourceAsync via reflection.
        if (string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName) ||
            string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
        {
            return;
        }

        // Ensure the referenced master profile exists to avoid breaking sites.
        if (await _indexProfileManager.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName) == null)
        {
            return;
        }

        ShellScope.AddDeferredTask(s =>
        {
            return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("migrate-datasource", dataSource, (scope, ds) =>
            {
                var indexingService = scope.ServiceProvider.GetRequiredService<DataSourceIndexingService>();

                return indexingService.SyncDataSourceAsync(ds);
            });
        });
    }

    /// <summary>
    /// Gets a property object from the Properties dictionary using string keys.
    /// </summary>
    private static JsonObject GetPropertyObject(JsonObject properties, string metadataClassName)
    {
        if (properties is null)
        {
            return null;
        }

        if (properties.TryGetPropertyValue(metadataClassName, out var node) && node is JsonObject obj)
        {
            return obj;
        }

        return null;
    }

    /// <summary>
    /// Gets a property value from the Properties dictionary using string keys.
    /// </summary>
    private static T GetPropertyValue<T>(JsonObject properties, string metadataClassName, string propertyName)
    {
        var obj = GetPropertyObject(properties, metadataClassName);
        if (obj is null)
        {
            return default;
        }

        if (obj.TryGetPropertyValue(propertyName, out var node) && node is not null)
        {
            return node.GetValue<T>();
        }

        return default;
    }

    /// <summary>
    /// Gets a nullable int value from a JsonObject.
    /// </summary>
    private static int? GetNullableInt(JsonObject obj, string propertyName)
    {
        if (obj.TryGetPropertyValue(propertyName, out var node) && node is not null)
        {
            try
            {
                return node.GetValue<int?>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }
}
