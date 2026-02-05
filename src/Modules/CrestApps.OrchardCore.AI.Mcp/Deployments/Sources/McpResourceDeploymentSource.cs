using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol.Protocol;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;

internal sealed class McpResourceDeploymentSource : DeploymentSourceBase<McpResourceDeploymentStep>
{
    private readonly ICatalog<McpResource> _store;

    public McpResourceDeploymentSource(ICatalog<McpResource> store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(McpResourceDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _store.GetAllAsync();

        var resourcesData = new JsonArray();

        var resourceIds = step.IncludeAll
            ? []
            : step.ResourceIds ?? [];

        foreach (var entry in entries)
        {
            if (resourceIds.Length > 0 && !resourceIds.Contains(entry.ItemId))
            {
                continue;
            }

            var resourceData = new JsonObject
            {
                { nameof(Resource.Uri), entry.Resource?.Uri },
                { nameof(Resource.Name), entry.Resource?.Name },
                { nameof(Resource.Title), entry.Resource?.Title },
                { nameof(Resource.Description), entry.Resource?.Description },
                { nameof(Resource.MimeType), entry.Resource?.MimeType },
            };

            var deploymentInfo = new JsonObject()
            {
                { nameof(McpResource.ItemId), entry.ItemId },
                { nameof(McpResource.Source), entry.Source },
                { nameof(McpResource.DisplayText), entry.DisplayText },
                { nameof(McpResource.Author), entry.Author },
                { nameof(McpResource.CreatedUtc), entry.CreatedUtc },
                { nameof(McpResource.OwnerId), entry.OwnerId },
                { nameof(McpResource.Resource), resourceData },
            };

            resourcesData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Resources"] = resourcesData,
        });
    }
}
