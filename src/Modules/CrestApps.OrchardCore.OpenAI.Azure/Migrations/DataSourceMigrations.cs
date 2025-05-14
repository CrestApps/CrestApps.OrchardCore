using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

[Obsolete("This migration will be removed before the release of v1")]
internal sealed class DataSourceMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    public DataSourceMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            // If the tenant is not created, nothing to migrate.
            return 2;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var profileManager = scope.ServiceProvider.GetRequiredService<INamedModelManager<AIProfile>>();
            var dataSourceManager = scope.ServiceProvider.GetRequiredService<IAIDataSourceManager>();

            var profiles = await profileManager.GetAllAsync();

            foreach (var profile in profiles)
            {
                if (profile.Source != "AzureAISearch")
                {
                    continue;
                }

                profile.Source = AzureOpenAIConstants.AzureOpenAIOwnData;

                if (profile.TryGet<AzureAIProfileAISearchMetadata>(out var metadata) && !string.IsNullOrEmpty(metadata.IndexName))
                {
                    var dataSource = await dataSourceManager.NewAsync(AzureOpenAIConstants.AzureOpenAIOwnData, AzureOpenAIConstants.DataSourceTypes.AzureAISearch);

                    dataSource.DisplayText = $"Azure OpenAI - {metadata.IndexName}";
                    dataSource.Put(metadata);

                    await dataSourceManager.CreateAsync(dataSource);

                    profile.Alter<AIProfileDataSourceMetadata>(m =>
                    {
                        m.DataSourceId = dataSource.Id;
                        m.DataSourceType = dataSource.Type;
                    });
                }

                await profileManager.UpdateAsync(profile);
            }
        });

        return 2;
    }

    public int UpdateFrom1()
    {
        if (_shellSettings.IsInitializing())
        {
            // If the tenant is not created, nothing to migrate.
            return 2;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var profileManager = scope.ServiceProvider.GetRequiredService<INamedModelManager<AIProfile>>();
            var dataSourceManager = scope.ServiceProvider.GetRequiredService<IAIDataSourceManager>();

            var profiles = await profileManager.GetAllAsync();

            foreach (var profile in profiles)
            {
                if (profile.Source != "AzureAISearch")
                {
                    continue;
                }

                profile.Source = AzureOpenAIConstants.AzureOpenAIOwnData;

                await profileManager.UpdateAsync(profile);
            }
        });

        return 2;
    }
}
