using System.Text.Json;
using CrestApps.Core.AI.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureOpenAIOwnDataAIDataSourceMigrations : DataMigration
{
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIOwnDataAIDataSourceMigrations"/> class.
    /// </summary>
    /// <param name="shellSettings">The shell settings.</param>
    public AzureOpenAIOwnDataAIDataSourceMigrations(ShellSettings shellSettings)
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
            return 5;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            // Previously, 'Azure' provider was different than 'AzureOpenAIOwnData', the two were merged into one.
            // Migrate legacy AzureAIDataSourceIndexMetadata to first-class properties.
            var dataSourceStore = scope.ServiceProvider.GetRequiredService<IAIDataSourceStore>();

            foreach (var dataSource in await dataSourceStore.GetAllAsync())
            {
                var needsUpdate = false;

                // Migrate legacy AzureAIDataSourceIndexMetadata to first-class fields.

                if (dataSource.Properties?.ContainsKey("AzureAIDataSourceIndexMetadata") == true)
                {
                    var propsJson = JsonSerializer.SerializeToNode(dataSource.Properties)?.AsObject();
                    var legacyIndex = propsJson?["AzureAIDataSourceIndexMetadata"];
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
}
