using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIToolInstanceDeploymentSource : DeploymentSourceBase<AIToolInstanceDeploymentStep>
{
    private readonly ICatalog<AIToolInstance> _store;

    public AIToolInstanceDeploymentSource(ICatalog<AIToolInstance> store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(AIToolInstanceDeploymentStep step, DeploymentPlanResult result)
    {
        var instances = await _store.GetAllAsync();

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

            var instanceObject = new JsonObject()
            {
                { "ItemId", instance.ItemId },
                { "Source", instance.Source },
                { "DisplayText", instance.DisplayText },
                { "CreatedUtc", instance.CreatedUtc },
                { "OwnerId", instance.OwnerId },
                { "Author", instance.Author },
                { "Properties", instance.Properties?.DeepClone() },
            };

            instanceObjects.Add(instanceObject);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["instances"] = instanceObjects,
        });
    }
}

