using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;

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
            return 4;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            // Previously, 'Azure' provider was different than 'AzureOpenAIOwnData', the two were merged into one.
            // Migrate legacy AzureAIDataSourceIndexMetadata to first-class properties.
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                var needsUpdate = false;

                // Migrate legacy AzureAIDataSourceIndexMetadata to first-class fields.
                if (dataSource.Has("AzureAIDataSourceIndexMetadata"))
                {
                    var legacyIndex = dataSource.Properties?["AzureAIDataSourceIndexMetadata"];
                    var indexName = legacyIndex?["IndexName"]?.GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(indexName) && string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
                    {
                        dataSource.SourceIndexProfileName = indexName;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    await dataSourceStore.UpdateAsync(dataSource);
                }
            }
        });

        return 4;
    }

    public async Task<int> UpdateFrom2Async()
    {
        if (_shellSettings.IsInitializing())
        {
            return 4;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                // Skip field mappings if already configured.
                if (!string.IsNullOrEmpty(dataSource.TitleFieldName) &&
                    !string.IsNullOrEmpty(dataSource.ContentFieldName))
                {
                    continue;
                }

                // Determine the source provider from legacy properties.
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
                    dataSource.TitleFieldName ??= "Content.ContentItem.DisplayText.Analyzed";
                    dataSource.ContentFieldName ??= "Content.ContentItem.FullText";
                }
                else if (string.Equals(providerName, "AzureAISearch", StringComparison.OrdinalIgnoreCase))
                {
                    dataSource.TitleFieldName ??= "Content__ContentItem__DisplayText__Analyzed";
                    dataSource.ContentFieldName ??= "Content__ContentItem__FullText";
                }

                await dataSourceStore.UpdateAsync(dataSource);
            }
        });

        return 4;
    }

    public int UpdateFrom3()
    {
        if (_shellSettings.IsInitializing())
        {
            return 4;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            // Set KeyFieldName for existing content-sourced data sources.
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                if (!string.IsNullOrEmpty(dataSource.KeyFieldName) ||
                    string.IsNullOrEmpty(dataSource.SourceIndexProfileName))
                {
                    continue;
                }

                var sourceProfile = await indexProfileStore.FindByNameAsync(dataSource.SourceIndexProfileName);

                if (sourceProfile != null &&
                    string.Equals(sourceProfile.Type, IndexingConstants.ContentsIndexSource, StringComparison.OrdinalIgnoreCase))
                {
                    dataSource.KeyFieldName = "ContentItemId";
                    await dataSourceStore.UpdateAsync(dataSource);
                }
            }
        });

        return 4;
    }
}
