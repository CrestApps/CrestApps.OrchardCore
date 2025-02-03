using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

public sealed class AIDeploymentDeploymentSource : DeploymentSourceBase<AIDeploymentDeploymentStep>
{
    private readonly IAIDeploymentStore _deploymentStore;

    public AIDeploymentDeploymentSource(IAIDeploymentStore deploymentStore)
    {
        _deploymentStore = deploymentStore;
    }

    protected override async Task ProcessAsync(AIDeploymentDeploymentStep step, DeploymentPlanResult result)
    {
        var deployments = await _deploymentStore.GetAllAsync();

        var deploymentData = new JsonArray();

        var deploymentNames = step.IncludeAll
            ? []
            : step.DeploymentNames ?? [];

        foreach (var deployment in deployments)
        {
            if (deploymentNames.Length > 0 && !deploymentNames.Contains(deployment.Name))
            {
                continue;
            }

            var deploymentInfo = new JsonObject()
            {
                { "Id", deployment.Id },
                { "Name", deployment.Name },
                { "ProviderName" , deployment.ProviderName },
                { "ConnectionName", deployment.ConnectionName },
                { "Author", deployment.Author },
                { "CreatedUtc" , deployment.CreatedUtc },
                { "OwnerId" , deployment.OwnerId },
            };

            var properties = new JsonObject();

            foreach (var property in deployment.Properties)
            {
                properties[property.Key] = property.Value.DeepClone();
            }

            deploymentInfo["Properties"] = properties;

            deploymentData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["deployments"] = deploymentData,
        });
    }
}
