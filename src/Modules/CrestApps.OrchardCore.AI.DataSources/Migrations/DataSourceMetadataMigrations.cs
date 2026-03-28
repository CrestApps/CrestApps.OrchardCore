using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.DataSources.Migrations;

internal sealed class DataSourceMetadataMigrations : DataMigration
{
    private const string LegacyKey = "AIProfileDataSourceMetadata";
    private const string NewKey = nameof(DataSourceMetadata);

    private readonly ShellSettings _shellSettings;

    public DataSourceMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    public int Create()
    {
        if (_shellSettings.IsInitializing())
        {
            return 1;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var profileStore = scope.ServiceProvider.GetRequiredService<IAIProfileStore>();

            foreach (var profile in await profileStore.GetAllAsync())
            {
                if (profile.Properties is null ||
                    !profile.Properties.ContainsKey(LegacyKey))
                {
                    continue;
                }

                var legacyNode = profile.Properties[LegacyKey];
                profile.Properties.Remove(LegacyKey);
                profile.Properties[NewKey] = legacyNode;

                await profileStore.UpdateAsync(profile);
            }
        });

        return 1;
    }
}
