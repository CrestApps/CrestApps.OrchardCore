using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIToolInstanceDeploymentSource : DeploymentSourceBase<AIToolInstanceDeploymentStep>
{
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentSource"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The catalog used to retrieve the AI tool instances to export.</param>
    public AIToolInstanceDeploymentSource(INamedCatalog<AIToolInstance> instancesCatalog)
    {
        _instancesCatalog = instancesCatalog;
    }

    protected override async Task ProcessAsync(AIToolInstanceDeploymentStep step, DeploymentPlanResult result)
    {
        var instances = await _instancesCatalog.GetAllAsync();

        var instanceObjects = new JsonArray();

        var instanceIds = step.IncludeAll
        ? []
        : step.InstanceIds ?? [];

        foreach (var instance in instances)
        {
            if (instanceIds.Length > 0 && !instanceIds.Contains(instance.ItemId))
            {
                continue;
            }

            instanceObjects.Add(new JsonObject()
            {
                { "ItemId", instance.ItemId },
                { "Source", instance.Source },
                { "Name", instance.Name },
                { "Description", instance.Description },
                { "CreatedUtc", instance.CreatedUtc },
                { "OwnerId", instance.OwnerId },
                { "Author", instance.Author },
                { "Properties", JsonSerializer.SerializeToNode(instance.Properties) },
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["instances"] = instanceObjects,
        });
    }
}
