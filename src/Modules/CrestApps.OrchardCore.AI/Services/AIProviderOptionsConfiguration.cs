using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class AIProviderOptionsConfiguration : IConfigureOptions<AIProviderOptions>
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ILogger _logger;

    public AIProviderOptionsConfiguration(
        IShellConfiguration shellConfiguration,
        ILogger<AIProviderOptionsConfiguration> logger)
    {
        _shellConfiguration = shellConfiguration;
        _logger = logger;
    }

    public void Configure(AIProviderOptions options)
    {
        var providerSettings = _shellConfiguration.GetSection("CrestApps_AI:Providers");

        if (providerSettings is null)
        {
            _logger.LogWarning("The 'providers' in 'CrestApps_AI:Providers' is not defined in the settings.");

            return;
        }

        try
        {
            var providerSettingsElements = JsonSerializer.Deserialize<JsonElement>(providerSettings.AsJsonNode());

            var providerSettingsObject = JsonObject.Create(providerSettingsElements);

            if (providerSettingsObject is null)
            {
                _logger.LogWarning("The 'providers' in 'CrestApps_AI:Providers' is invalid.");

                return;
            }

            foreach (var providerPair in providerSettingsObject)
            {
                var providerName = providerPair.Key;
                var providerNode = providerPair.Value;

                var connectionsNode = providerNode["Connections"];

                if (connectionsNode is null)
                {
                    _logger.LogWarning("The provider with the name '{Name}' has no connections. This provider will be ignore and not used.", providerName);

                    continue;
                }

                var collectionsElement = JsonSerializer.Deserialize<JsonElement>(connectionsNode);

                var connectionsObject = JsonObject.Create(collectionsElement);

                if (connectionsObject is null || connectionsObject.Count == 0)
                {
                    _logger.LogWarning("The provider with the name '{Name}' has no connection. This provider will be ignore and not used.", providerName);

                    continue;
                }

                var connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (var connectionPair in connectionsObject)
                {
                    connections.Add(connectionPair.Key, connectionPair.Value.Deserialize<AIProviderConnectionEntry>());
                }

                if (connections.Count == 0)
                {
                    _logger.LogWarning("The provider with the name '{Name}' has no valid connections. This provider will be ignore and not used.", providerName);

                    continue;
                }

                var provider = new AIProvider()
                {
                    Connections = connections,
                };

                var defaultConnectionName = providerNode["DefaultConnectionName"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(defaultConnectionName))
                {
                    provider.DefaultConnectionName = defaultConnectionName;
                }
                else
                {
                    provider.DefaultConnectionName = connections.FirstOrDefault().Key;
                }

                var defaultDeploymentName = providerNode["DefaultChatDeploymentName"]?.GetValue<string>()
                    ?? providerNode["DefaultDeploymentName"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(defaultDeploymentName))
                {
                    provider.DefaultChatDeploymentName = defaultDeploymentName;
                }
                else
                {
                    provider.DefaultChatDeploymentName = connections.FirstOrDefault().Value?.GetChatDeploymentOrDefaultName(false);
                }

                var defaultEmbeddingDeploymentName = providerNode["DefaultEmbeddingDeploymentName"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(defaultEmbeddingDeploymentName))
                {
                    provider.DefaultEmbeddingDeploymentName = defaultEmbeddingDeploymentName;
                }

                var defaultImagesDeploymentName = providerNode["DefaultImagesDeploymentName"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(defaultImagesDeploymentName))
                {
                    provider.DefaultImagesDeploymentName = defaultImagesDeploymentName;
                }

                var defaultUtilityDeploymentName = providerNode["DefaultUtilityDeploymentName"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(defaultUtilityDeploymentName))
                {
                    provider.DefaultUtilityDeploymentName = defaultUtilityDeploymentName;
                }

                options.Providers.Add(providerName, provider);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid 'CrestApps_AI:Providers' configuration. Please refer to the documentation for instructions on how to set it up correctly.");
        }
    }
}
