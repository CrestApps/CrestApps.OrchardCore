using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureOpenAIOwnDataAIDataSourceMigrations : DataMigration
{
#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            // Previously, 'Azure' provider was different than 'AzureOpenAIOwnData', the two were merged into one.
            // So we want to change the source to merge them in the database too to ensure backward compatibility.
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<IAIDataSourceStore>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                if (!dataSource.ProfileSource.Equals("AzureOpenAIOwnData", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                dataSource.ProfileSource = AzureOpenAIConstants.ProviderName;

                await dataSourceStore.UpdateAsync(dataSource);
            }
        });

        return 1;
    }
}

