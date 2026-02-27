using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Migrations;

internal sealed class DataSourceMetadataMigrations : DataMigration
{
    private const string LegacyKey = "ChatInteractionDataSourceMetadata";
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
            var interactionStore = scope.ServiceProvider.GetRequiredService<ICatalog<ChatInteraction>>();

            foreach (var interaction in await interactionStore.GetAllAsync())
            {
                if (interaction.Properties is null ||
                    !interaction.Properties.ContainsKey(LegacyKey))
                {
                    continue;
                }

                var legacyNode = interaction.Properties[LegacyKey];
                interaction.Properties.Remove(LegacyKey);
                interaction.Properties[NewKey] = legacyNode;

                await interactionStore.UpdateAsync(interaction);
            }
        });

        return 1;
    }
}
