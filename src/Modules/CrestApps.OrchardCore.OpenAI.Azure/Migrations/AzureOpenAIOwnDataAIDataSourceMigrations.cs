using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

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
            return 5;
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

        return 5;
    }

    public Task<int> UpdateFrom2Async()
    {
        if (_shellSettings.IsInitializing())
        {
            return Task.FromResult(5);
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<ICatalog<AIDataSource>>();
            var indexProfileStore = scope.ServiceProvider.GetRequiredService<IIndexProfileStore>();
            var indexProfileManager = scope.ServiceProvider.GetRequiredService<IIndexProfileManager>();

            await AzureOpenAIDataSourceMetadataMigrations.MigrateKnowledgeBaseIndexesAsync(
                dataSourceStore,
                indexProfileStore,
                indexProfileManager,
                scope.ServiceProvider);
        });

        return Task.FromResult(4);
    }
}
