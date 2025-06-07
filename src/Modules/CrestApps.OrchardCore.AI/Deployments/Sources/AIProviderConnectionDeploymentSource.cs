using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProviderConnectionDeploymentSource : DeploymentSourceBase<AIProviderConnectionDeploymentStep>
{
    private readonly INamedCatalog<AIProviderConnection> _store;
    private readonly IEnumerable<IAIProviderConnectionHandler> _handlers;
    private readonly ILogger _logger;

    public AIProviderConnectionDeploymentSource(
        INamedCatalog<AIProviderConnection> store,
        IEnumerable<IAIProviderConnectionHandler> handlers,
        ILogger<AIProviderConnectionDeploymentSource> logger)
    {
        _store = store;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(AIProviderConnectionDeploymentStep step, DeploymentPlanResult result)
    {
        var connections = await _store.GetAllAsync();

        var connectionObjects = new JsonArray();

        var connectionIds = step.IncludeAll
            ? []
            : step.ConnectionIds ?? [];

        foreach (var connection in connections)
        {
            if (connectionIds.Length > 0 && !connectionIds.Contains(connection.Id))
            {
                continue;
            }

            var connectionObject = new JsonObject()
            {
                { "Id", connection.Id },
                { "Source", connection.Source },
                { "Name", connection.Name },
                { "DefaultDeploymentName", connection.DefaultDeploymentName },
                { "IsDefault", connection.IsDefault },
                { "DisplayText", connection.DisplayText },
                { "CreatedUtc", connection.CreatedUtc },
                { "OwnerId", connection.OwnerId },
                { "Author", connection.Author },
                { "Properties", connection.Properties?.DeepClone() },
            };

            var exportingContext = new ExportingAIProviderConnectionContext(connection, connectionObject);

            _handlers.Invoke((handler, context) => handler.Exporting(context), exportingContext, _logger);

            connectionObjects.Add(connectionObject);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["connections"] = connectionObjects,
        });
    }
}

