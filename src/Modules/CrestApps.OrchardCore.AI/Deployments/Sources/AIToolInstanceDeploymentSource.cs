using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

public sealed class AIToolInstanceDeploymentSource : DeploymentSourceBase<AIToolInstanceDeploymentStep>
{
    private readonly IAIToolInstanceStore _store;

    public AIToolInstanceDeploymentSource(IAIToolInstanceStore store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(AIToolInstanceDeploymentStep step, DeploymentPlanResult result)
    {
        var instances = await _store.GetAllAsync();

        var instancesData = new JsonArray();

        var instanceIds = step.IncludeAll
            ? []
            : step.InstanceIds ?? [];

        foreach (var instance in instances)
        {
            if (instanceIds.Length > 0 && !instanceIds.Contains(instance.Id))
            {
                continue;
            }

            var instanceInfo = new JsonObject()
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

            instanceInfo["Properties"] = properties;

            instancesData.Add(instanceInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["instances"] = instancesData,
        });
    }
}
