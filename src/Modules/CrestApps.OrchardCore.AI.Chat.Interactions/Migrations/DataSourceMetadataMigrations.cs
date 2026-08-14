using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="DataSourceMetadataMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    public DataSourceMetadataMigrations(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Creates a new .
    /// </summary>
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
