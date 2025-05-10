using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

internal sealed class McpConnectionDeploymentSource : DeploymentSourceBase<McpConnectionDeploymentStep>
{
    private readonly ISourceModelStore<McpConnection> _store;

    public McpConnectionDeploymentSource(ISourceModelStore<McpConnection> store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(McpConnectionDeploymentStep step, DeploymentPlanResult result)
    {
        var connections = await _store.GetAllAsync();

        var connectionsData = new JsonArray();

        var connectionIds = step.IncludeAll
            ? []
            : step.ConnectionIds ?? [];

        foreach (var connection in connections)
        {
            if (connectionIds.Length > 0 && !connectionIds.Contains(connection.Id))
            {
                continue;
            }

            var deploymentInfo = new JsonObject()
            {
                { "Id", connection.Id },
                { "DisplayText", connection.DisplayText },
                { "Author", connection.Author },
                { "CreatedUtc" , connection.CreatedUtc },
                { "OwnerId" , connection.OwnerId },
            };

            var properties = new JsonObject();

            foreach (var property in connection.Properties)
            {
                properties[property.Key] = property.Value.DeepClone();
            }

            deploymentInfo["Properties"] = properties;

            connectionsData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["connections"] = connectionsData,
        });
    }
}
