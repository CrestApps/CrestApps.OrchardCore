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
            return 1;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var profileManager = scope.ServiceProvider.GetRequiredService<INamedModelManager<AIProfile>>();
            var dataSourceManager = scope.ServiceProvider.GetRequiredService<IAIDataSourceManager>();
            // get all profiles.
            var profiles = await profileManager.GetAllAsync();

            foreach (var profile in profiles)
            {
                if (!profile.TryGet<AzureAIProfileAISearchMetadata>(out var metadata))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(metadata.IndexName))
                {
                    continue;
                }

                var dataSource = await dataSourceManager.NewAsync(AzureOpenAIConstants.AISearchImplementationName, "azure_search");

                dataSource.DisplayText = $"Azure OpenAI - {metadata.IndexName}";
                dataSource.Put(metadata);

                await dataSourceManager.CreateAsync(dataSource);

                profile.Alter<AIProfileDataSourceMetadata>(m =>
                {
                    m.DataSourceId = dataSource.Id;
                    m.DataSourceType = dataSource.Type;
                });

                await profileManager.UpdateAsync(profile);
            }

        });

        return 1;
    }
}
