using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIToolInstanceDeploymentSource : DeploymentSourceBase<AIToolInstanceDeploymentStep>
{
    private readonly IModelStore<AIToolInstance> _store;

    public AIToolInstanceDeploymentSource(IModelStore<AIToolInstance> store)
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
            if (instanceIds.Length > 0 && !instanceIds.Contains(instance.Id))
            {
                continue;
            }

            var instanceObject = new JsonObject()
            {
                { "Id", instance.Id },
                { "Source", instance.Source },
                { "DisplayText", instance.DisplayText },
                { "CreatedUtc", instance.CreatedUtc },
                { "OwnerId", instance.OwnerId },
                { "Author", instance.Author },
            };

            var properties = new JsonObject();

            foreach (var property in instance.Properties)
            {
                properties[property.Key] = property.Value.DeepClone();
            }

            instanceObject["Properties"] = properties;

            instanceObjects.Add(instanceObject);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["instances"] = instanceObjects,
        });
    }
}
