using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

/// <summary>
/// Migrates existing Azure AI data source metadata from the legacy format to the new format.
/// This migration:
/// 1. Extracts IndexName from legacy metadata and stores it in AzureAIDataSourceIndexMetadata on the data source
/// 2. Extracts query-time parameters (Filter, Strictness, TopNDocuments) and stores them in AzureRagChatMetadata on AI profiles
/// </summary>
internal sealed class AzureOpenAIDataSourceMetadataMigrations : DataMigration
{
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<IAIDataSourceStore>();
            var profileStore = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIProfile>>();

            // Migrate data sources to use the new index metadata
            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName)
                {
                    continue;
                }

                var needsUpdate = false;
                string indexName = null;

                // Check if new metadata already exists
                var newIndexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();
                if (!string.IsNullOrWhiteSpace(newIndexMetadata?.IndexName))
                {
                    // Already migrated
                    continue;
                }

                // Extract IndexName based on data source type
                switch (dataSource.Type)
                {
                    case AzureOpenAIConstants.DataSourceTypes.AzureAISearch:
                        var aiSearchMetadata = dataSource.As<AzureAIProfileAISearchMetadata>();
                        if (aiSearchMetadata is not null && !string.IsNullOrWhiteSpace(aiSearchMetadata.IndexName))
                        {
                            indexName = aiSearchMetadata.IndexName;
                            needsUpdate = true;
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.Elasticsearch:
                        var esMetadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();
                        if (esMetadata is not null && !string.IsNullOrWhiteSpace(esMetadata.IndexName))
                        {
                            indexName = esMetadata.IndexName;
                            needsUpdate = true;
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.MongoDB:
                        var mongoMetadata = dataSource.As<AzureAIProfileMongoDBMetadata>();
                        if (mongoMetadata is not null && !string.IsNullOrWhiteSpace(mongoMetadata.IndexName))
                        {
                            // For MongoDB, migrate to the new metadata class
                            var newMongoMetadata = new AzureMongoDBDataSourceMetadata
                            {
                                IndexName = mongoMetadata.IndexName,
                                EndpointName = mongoMetadata.EndpointName,
                                AppName = mongoMetadata.AppName,
                                CollectionName = mongoMetadata.CollectionName,
                                DatabaseName = mongoMetadata.DatabaseName,
                                Authentication = mongoMetadata.Authentication,
                            };
                            dataSource.Put(newMongoMetadata);
                            needsUpdate = true;
                        }
                        break;
                }

                if (needsUpdate && !string.IsNullOrWhiteSpace(indexName))
                {
                    // Store the index name in the new metadata
                    dataSource.Put(new AzureAIDataSourceIndexMetadata
                    {
                        IndexName = indexName,
                    });
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

                var dataSourceMetadata = profile.As<AIProfileDataSourceMetadata>();
                if (string.IsNullOrEmpty(dataSourceMetadata?.DataSourceId))
                {
                    continue;
                }

                // Check if new RAG metadata already exists
                var existingRagMetadata = profile.As<AzureRagChatMetadata>();
                if (existingRagMetadata is not null &&
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

                // Extract query parameters based on data source type
                switch (dataSource.Type)
                {
                    case AzureOpenAIConstants.DataSourceTypes.AzureAISearch:
                        var aiSearchMetadata = dataSource.As<AzureAIProfileAISearchMetadata>();
                        if (aiSearchMetadata is not null)
                        {
                            strictness = aiSearchMetadata.Strictness;
                            topNDocuments = aiSearchMetadata.TopNDocuments;
                            filter = aiSearchMetadata.Filter;
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.Elasticsearch:
                        var esMetadata = dataSource.As<AzureAIProfileElasticsearchMetadata>();
                        if (esMetadata is not null)
                        {
                            strictness = esMetadata.Strictness;
                            topNDocuments = esMetadata.TopNDocuments;
                            filter = esMetadata.Filter;
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.MongoDB:
                        var mongoMetadata = dataSource.As<AzureAIProfileMongoDBMetadata>();
                        if (mongoMetadata is not null)
                        {
                            strictness = mongoMetadata.Strictness;
                            topNDocuments = mongoMetadata.TopNDocuments;
                        }
                        break;
                }

                // Store query parameters on the profile
                if (strictness.HasValue || topNDocuments.HasValue || !string.IsNullOrWhiteSpace(filter))
                {
                    profile.Put(new AzureRagChatMetadata
                    {
                        Strictness = strictness,
                        TopNDocuments = topNDocuments,
                        Filter = filter,
                    });

                    await profileStore.UpdateAsync(profile);
                }
            }
        });

        return 1;
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
