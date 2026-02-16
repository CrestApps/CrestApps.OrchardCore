using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureOpenAIOwnDataAIDataSourceMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public AzureOpenAIOwnDataAIDataSourceMigrations(ShellSettings shellSettings)
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
            // Previously, 'Azure' provider was different than 'AzureOpenAIOwnData', the two were merged into one.
            // Migrate legacy AzureAIDataSourceIndexMetadata to AIDataSourceIndexMetadata.
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                var needsUpdate = false;

                // Migrate legacy AzureAIDataSourceIndexMetadata to AIDataSourceIndexMetadata.
                if (dataSource.Has("AzureAIDataSourceIndexMetadata"))
                {
                    var legacyIndex = dataSource.Properties?["AzureAIDataSourceIndexMetadata"];
                    var indexName = legacyIndex?["IndexName"]?.GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(indexName))
                    {
                        dataSource.Put(new AIDataSourceIndexMetadata
                        {
                            IndexName = indexName,
                        });
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    await dataSourceStore.UpdateAsync(dataSource);
                }
            }
        });

        return 3;
    }

    public async Task<int> UpdateFrom2Async()
    {
        if (_shellSettings.IsInitializing())
        {
            return 3;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                var indexMetadata = dataSource.As<AIDataSourceIndexMetadata>();

                // Skip field mappings if already configured.
                if (!string.IsNullOrEmpty(indexMetadata.TitleFieldName) &&
                    !string.IsNullOrEmpty(indexMetadata.ContentFieldName))
                {
                    continue;
                }

                // Determine the source provider from the source index profile.
                // Use the ProviderName from legacy properties or the source index name.
                var providerName = dataSource.Properties?["ProviderName"]?.GetValue<string>();

                if (string.IsNullOrEmpty(providerName))
                {
                    var profileSource = dataSource.Properties?["ProfileSource"]?.GetValue<string>();
                    var type = dataSource.Properties?["Type"]?.GetValue<string>();

                    if (!string.IsNullOrEmpty(profileSource))
                    {
                        providerName = type switch
                        {
                            "elasticsearch" => "Elasticsearch",
                            "azure_search" => "AzureAISearch",
                            _ => type,
                        };
                    }
                }

                if (string.IsNullOrEmpty(providerName))
                {
                    continue;
                }

                // Set default field mappings based on the provider.
                if (string.Equals(providerName, "Elasticsearch", StringComparison.OrdinalIgnoreCase))
                {
                    indexMetadata.TitleFieldName ??= "Content.ContentItem.DisplayText.Analyzed";
                    indexMetadata.ContentFieldName ??= "Content.ContentItem.FullText";
                }
                else if (string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
                {
                    indexMetadata.TitleFieldName ??= "Content__ContentItem__DisplayText__Analyzed";
                    indexMetadata.ContentFieldName ??= "Content__ContentItem__FullText";
                }

                dataSource.Put(indexMetadata);
                await dataSourceStore.UpdateAsync(dataSource);
            }
        });

        return 3;
    }
}
