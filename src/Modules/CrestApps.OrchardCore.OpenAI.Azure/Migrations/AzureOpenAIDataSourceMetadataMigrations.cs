using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundJobs;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;

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

    private readonly ShellSettings _shellSettings;

    public AzureOpenAIDataSourceMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
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
                if (profile.Source != AzureOpenAIConstants.ProviderName)
                {
                    continue;
                }

                if (profile.Source == "AzureOpenAIOwnData")
                {
                    profile.Source = "Azure";
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
            return 2;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();
            var indexProfileManager = scope.ServiceProvider.GetRequiredService<IIndexProfileManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AzureOpenAIDataSourceMetadataMigrations>>();

            var dataSources = await dataSourceStore.GetAllAsync();

            if (dataSources.Count == 0)
            {
                return;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            var first = dataSources
                .Where(x => x.ProfileSource.StartsWith("Azure", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type == "elasticsearch" ? 0 : 1)
                .FirstOrDefault();
#pragma warning restore CS0618 // Type or member is obsolete

            if (first == null)
            {
                logger.LogWarning("No Elasticsearch or Azure data sources available. Can't migrate data sources.");

                return;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            var firstProviderName = first.ProfileSource;

            if (firstProviderName == "Azure" || firstProviderName == "AzureOpenAIOwnData")
            {
                firstProviderName = "AzureAISearch";
            }

            if (first.Type == "elasticsearch")
            {
                firstProviderName = "Elasticsearch";
            }

#pragma warning restore CS0618 // Type or member is obsolete

            var masterIndexUniqueName = "AI Knowledge Base Warehouse";

            var knowledgeBaseIndex = await indexProfileManager.FindByNameAsync(masterIndexUniqueName);

            if (knowledgeBaseIndex is null)
            {
                knowledgeBaseIndex = await indexProfileManager.NewAsync(firstProviderName, DataSourceConstants.IndexingTaskType, new JsonObject
                {
                    { "Name", masterIndexUniqueName },
                    { "IndexName", "AIKnowledgeBaseWarehouse" },
                });

                var indexManager = scope.ServiceProvider.GetKeyedService<IIndexManager>(firstProviderName);

                if (indexManager is null)
                {
                    logger.LogWarning("Unable to create knowledge base index due to a missing index-manager");
                    return;
                }

                await indexProfileManager.CreateAsync(knowledgeBaseIndex);

                if (!await indexManager.ExistsAsync(knowledgeBaseIndex.IndexFullName) && !await indexManager.CreateAsync(knowledgeBaseIndex))
                {
                    logger.LogWarning("Unable to create a knowledge base index in the provider.");

                    await indexProfileManager.DeleteAsync(knowledgeBaseIndex);

                    return;
                }
            }

            foreach (var dataSource in dataSources)
            {
                if (!string.IsNullOrEmpty(dataSource.SourceIndexProfileName) && !string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
                {
                    // No need to migrate this data source.
                    continue;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                var providerName = dataSource.ProfileSource;
#pragma warning restore CS0618 // Type or member is obsolete

                if (string.IsNullOrEmpty(providerName))
                {
                    continue;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                if (providerName == "Azure" || providerName == "AzureOpenAIOwnData")
                {
                    dataSource.ProfileSource = "AzureAISearch";
                }

                if (dataSource.Type == "elasticsearch")
                {
                    providerName = "Elasticsearch";
                }

#pragma warning restore CS0618 // Type or member is obsolete

                var azureIndexName = dataSource.Properties[LegacyAISearchMetadataName]?["IndexName"]?.ToString();

                if (!string.IsNullOrEmpty(azureIndexName))
                {
                    dataSource.SourceIndexProfileName = azureIndexName;
                }

                var elasticsearchIndexName = dataSource.Properties[LegacyElasticsearchMetadataName]?["IndexName"]?.ToString();

                if (!string.IsNullOrEmpty(elasticsearchIndexName))
                {
                    dataSource.SourceIndexProfileName = elasticsearchIndexName;
                }

                if (string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
                {
                    var newMetadata = dataSource.Properties["AzureAIDataSourceIndexMetadata"]?["IndexName"]?.ToString();

                    if (!string.IsNullOrEmpty(newMetadata))
                    {
                        dataSource.SourceIndexProfileName = newMetadata;
                    }
                }

                if (string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
                {
                    logger.LogWarning("Unable to determine the Source Index profile name for data source {Name}. Ignoring it's migration", dataSource.DisplayText);

                    continue;
                }

                dataSource.AIKnowledgeBaseIndexProfileName = knowledgeBaseIndex.Name;

                // Ensure a KeyFieldName for content-sourced indexes.
                if (!string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
                {
                    var sourceProfile = await indexProfileStore.FindByNameAsync(dataSource.SourceIndexProfileName);

                    if (sourceProfile != null &&
                        string.Equals(sourceProfile.Type, IndexingConstants.ContentsIndexSource, StringComparison.OrdinalIgnoreCase))
                    {
                        dataSource.KeyFieldName = "ContentItemId";

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
                        }
                    }
                }

                await dataSourceStore.UpdateAsync(dataSource);

                ShellScope.AddDeferredTask(s =>
                {
                    return HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("sync-datasource", async scope =>
                    {
                        var indexingService = scope.ServiceProvider.GetRequiredService<DataSourceIndexingService>();

                        await indexingService.SyncDataSourceAsync(dataSource);
                    });
                });
            }
        });

        return 2;
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
