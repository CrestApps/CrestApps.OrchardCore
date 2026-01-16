using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
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
    // Legacy metadata class names (used for Properties dictionary access)
    private const string LegacyAISearchMetadataName = "AzureAIProfileAISearchMetadata";
    private const string LegacyElasticsearchMetadataName = "AzureAIProfileElasticsearchMetadata";
    private const string LegacyMongoDBMetadataName = "AzureAIProfileMongoDBMetadata";

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

                // Check if new metadata already exists
                var newIndexMetadata = dataSource.As<AzureAIDataSourceIndexMetadata>();
                if (!string.IsNullOrWhiteSpace(newIndexMetadata?.IndexName))
                {
                    // Already migrated
                    continue;
                }

                // Extract IndexName based on data source type using Properties dictionary
                switch (dataSource.Type)
                {
                    case AzureOpenAIConstants.DataSourceTypes.AzureAISearch:
                        {
                            var indexName = GetPropertyValue<string>(dataSource.Properties, LegacyAISearchMetadataName, "IndexName");
                            if (!string.IsNullOrWhiteSpace(indexName))
                            {
                                dataSource.Put(new AzureAIDataSourceIndexMetadata
                                {
                                    IndexName = indexName,
                                });
                                needsUpdate = true;
                            }
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.Elasticsearch:
                        {
                            var indexName = GetPropertyValue<string>(dataSource.Properties, LegacyElasticsearchMetadataName, "IndexName");
                            if (!string.IsNullOrWhiteSpace(indexName))
                            {
                                dataSource.Put(new AzureAIDataSourceIndexMetadata
                                {
                                    IndexName = indexName,
                                });
                                needsUpdate = true;
                            }
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.MongoDB:
                        {
                            var legacyProps = GetPropertyObject(dataSource.Properties, LegacyMongoDBMetadataName);

                            if (legacyProps != null)
                            {
                                var indexName = legacyProps["IndexName"]?.GetValue<string>();

                                if (!string.IsNullOrWhiteSpace(indexName))
                                {
                                    dataSource.Put(new AzureAIDataSourceIndexMetadata
                                    {
                                        IndexName = indexName,
                                    });

                                    // For MongoDB, migrate to the new metadata class
                                    var authProps = legacyProps["Authentication"]?.AsObject();

                                    AzureAIProfileMongoDBAuthenticationType auth = null;

                                    if (authProps != null)
                                    {
                                        auth = new AzureAIProfileMongoDBAuthenticationType
                                        {
                                            Type = authProps["Type"]?.GetValue<string>(),
                                            Username = authProps["Username"]?.GetValue<string>(),
                                            Password = authProps["Password"]?.GetValue<string>(),
                                        };
                                    }

                                    var newMongoMetadata = new AzureMongoDBDataSourceMetadata
                                    {
                                        EndpointName = legacyProps["EndpointName"]?.GetValue<string>(),
                                        AppName = legacyProps["AppName"]?.GetValue<string>(),
                                        CollectionName = legacyProps["CollectionName"]?.GetValue<string>(),
                                        DatabaseName = legacyProps["DatabaseName"]?.GetValue<string>(),
                                        Authentication = auth,
                                    };

                                    dataSource.Put(newMongoMetadata);
                                    needsUpdate = true;
                                }
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
                if (profile.TryGet<AzureRagChatMetadata>(out var existingRagMetadata) &&
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

                // Extract query parameters based on data source type using Properties dictionary
                switch (dataSource.Type)
                {
                    case AzureOpenAIConstants.DataSourceTypes.AzureAISearch:
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

                    case AzureOpenAIConstants.DataSourceTypes.Elasticsearch:
                        {
                            var legacyProps = GetPropertyObject(dataSource.Properties, LegacyElasticsearchMetadataName);
                            if (legacyProps != null)
                            {
                                strictness = GetNullableInt(legacyProps, "Strictness");
                                topNDocuments = GetNullableInt(legacyProps, "TopNDocuments");
                            }
                        }
                        break;

                    case AzureOpenAIConstants.DataSourceTypes.MongoDB:
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
                    profile.Put(new AzureRagChatMetadata
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

        return 1;
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
                // Value cannot be converted to int - return null
                return null;
            }
            catch (FormatException)
            {
                // Value is not in the expected format - return null
                return null;
            }
        }

        return null;
    }
}
