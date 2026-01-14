using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureOpenAIOwnDataAIProfilesMigrations : DataMigration
{
#pragma warning disable CA1822 // Mark members as static
    public int Create()
#pragma warning restore CA1822 // Mark members as static
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            // Previously, 'Azure' provider was different than 'AzureOpenAIOwnData', the two were merged into one.
            // So we want to change the source to merge them in the database too to ensure backward compatibility.
            var profileStore = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIProfile>>();

            foreach (var profile in await profileStore.GetAllAsync())
            {
                if (!profile.Source.Equals("AzureOpenAIOwnData", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                profile.Source = AzureOpenAIConstants.ProviderName;

                await profileStore.UpdateAsync(profile);
            }
        });

        return 1;
    }
}

