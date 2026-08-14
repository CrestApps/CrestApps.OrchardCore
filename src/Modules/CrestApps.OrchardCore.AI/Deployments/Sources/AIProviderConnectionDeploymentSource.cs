using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProviderConnectionDeploymentSource : DeploymentSourceBase<AIProviderConnectionDeploymentStep>
{
    private readonly INamedCatalog<AIProviderConnection> _connectionsCatalog;
    private readonly IEnumerable<IAIProviderConnectionHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDeploymentSource"/> class.
    /// </summary>
    /// <param name="connectionsCatalog">The catalog for retrieving AI provider connections by name.</param>
    /// <param name="handlers">The collection of handlers invoked during connection export.</param>
    /// <param name="logger">The logger instance for this source.</param>
    public AIProviderConnectionDeploymentSource(
        INamedCatalog<AIProviderConnection> connectionsCatalog,
        IEnumerable<IAIProviderConnectionHandler> handlers,
        ILogger<AIProviderConnectionDeploymentSource> logger)
    {
        _connectionsCatalog = connectionsCatalog;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(AIProviderConnectionDeploymentStep step, DeploymentPlanResult result)
    {
        var connections = await _connectionsCatalog.GetAllAsync();

        var connectionObjects = new JsonArray();

        var connectionIds = step.IncludeAll
        ? []
        : step.ConnectionIds ?? [];

        foreach (var connection in connections)
        {
            if (connectionIds.Length > 0 && !connectionIds.Contains(connection.ItemId))
            {
                continue;
            }

            var connectionObject = new JsonObject()
            {
                { "ItemId", connection.ItemId },
                { "Source", connection.Source },
                { "Name", connection.Name },
                { "DisplayText", connection.DisplayText },
                { "CreatedUtc", connection.CreatedUtc },
                { "OwnerId", connection.OwnerId },
                { "Author", connection.Author },
                { "Properties", JsonSerializer.SerializeToNode(connection.Properties) },
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
