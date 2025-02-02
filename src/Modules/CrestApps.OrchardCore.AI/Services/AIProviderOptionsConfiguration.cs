using System.Text.Json;
using System.Text.Json.Nodes;
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
        var jsonNode = _shellConfiguration.GetSection("CrestApps_AI:Providers").AsJsonNode();

        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonNode);

        var jsonProviders = JsonObject.Create(jsonElement, new JsonNodeOptions()
        {
            PropertyNameCaseInsensitive = true,
        });

        var providers = new Dictionary<string, AIProvider>();

        foreach (var jsonProvider in jsonProviders)
        {
            var providerName = jsonProvider.Key;
            var providerValue = jsonProvider.Value;

            var connectionsNode = providerValue["Connections"];

            var collectionsElement = JsonSerializer.Deserialize<JsonElement>(connectionsNode);

            var jsonConnections = JsonObject.Create(collectionsElement, new JsonNodeOptions()
            {
                PropertyNameCaseInsensitive = true,
            });

            if (jsonConnections is null || jsonConnections.Count == 0)
            {
                _logger.LogWarning("The provider with the name '{Name}' has no connection. This provider will be ignore and not used.", providerName);

                continue;
            }

            var connections = new Dictionary<string, AIProviderConnection>();

            foreach (var jsonConnection in jsonConnections)
            {
                connections.Add(jsonConnection.Key, jsonConnection.Value.Deserialize<AIProviderConnection>());
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

            var defaultConnectionName = providerValue["DefaultConnectionName"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(defaultConnectionName))
            {
                provider.DefaultConnectionName = defaultConnectionName;
            }
            else
            {
                provider.DefaultConnectionName = connections.FirstOrDefault().Key;
            }

            var defaultDeploymentName = providerValue["DefaultDeploymentName"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(defaultDeploymentName))
            {
                provider.DefaultDeploymentName = defaultDeploymentName;
            }
            else
            {
                provider.DefaultDeploymentName = connections.FirstOrDefault().Value["DefaultDeploymentName"] as string;
            }

            providers.Add(providerName, provider);
        }

        options.Providers = providers;
    }
}
