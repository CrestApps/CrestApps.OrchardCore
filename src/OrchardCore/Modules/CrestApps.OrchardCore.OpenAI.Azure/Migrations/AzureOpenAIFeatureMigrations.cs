using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

/// <summary>
/// Migrates old Azure OpenAI data source feature IDs to new provider-agnostic feature IDs.
/// </summary>
internal sealed class AzureOpenAIFeatureMigrations : DataMigration
{
    private const string OldAISearchFeature = "CrestApps.OrchardCore.OpenAI.Azure.AISearch";
    private const string OldElasticsearchFeature = "CrestApps.OrchardCore.OpenAI.Azure.Elasticsearch";
    private const string OldMongoDBFeature = "CrestApps.OrchardCore.OpenAI.Azure.MongoDB";
    private const string OldStandardFeature = "CrestApps.OrchardCore.OpenAI.Azure.Standard";

    private readonly ShellSettings _shellSettings;

    public AzureOpenAIFeatureMigrations(ShellSettings shellSettings)
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
            var shellFeaturesManager = scope.ServiceProvider.GetRequiredService<IShellFeaturesManager>();
            var extensionManager = scope.ServiceProvider.GetRequiredService<IExtensionManager>();

            var enabledFeatures = await shellFeaturesManager.GetEnabledFeaturesAsync();
            var enabledFeatureIds = enabledFeatures.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var featuresToEnable = new List<IFeatureInfo>();

            // Map old feature IDs to new feature IDs.
            var featureMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { OldAISearchFeature, AIConstants.Feature.DataSourceAzureAI },
                { OldElasticsearchFeature, AIConstants.Feature.DataSourceElasticsearch },
                { OldMongoDBFeature, AIConstants.Feature.DataSourceMongoDB },
            };

            foreach (var (oldFeatureId, newFeatureId) in featureMapping)
            {
                if (enabledFeatureIds.Contains(oldFeatureId) && !enabledFeatureIds.Contains(newFeatureId))
                {
                    var newFeature = extensionManager.GetFeatures((IEnumerable<string>)[newFeatureId]).FirstOrDefault();

                    if (newFeature != null)
                    {
                        featuresToEnable.Add(newFeature);
                    }
                }
            }

            // If the old Standard feature was enabled, ensure the main Area feature is enabled.
            if (enabledFeatureIds.Contains(OldStandardFeature) && !enabledFeatureIds.Contains(AzureOpenAIConstants.Feature.Area))
            {
                var areaFeature = extensionManager.GetFeatures((IEnumerable<string>)[AzureOpenAIConstants.Feature.Area]).FirstOrDefault();

                if (areaFeature != null)
                {
                    featuresToEnable.Add(areaFeature);
                }
            }

            if (featuresToEnable.Count > 0)
            {
                await shellFeaturesManager.EnableFeaturesAsync(featuresToEnable, force: true);
            }
        });

        return 1;
    }
}
