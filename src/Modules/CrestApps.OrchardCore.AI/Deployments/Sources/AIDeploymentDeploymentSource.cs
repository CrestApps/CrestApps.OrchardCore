using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIDeploymentDeploymentSource : DeploymentSourceBase<AIDeploymentDeploymentStep>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIDeploymentDeploymentSource(INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    protected override async Task ProcessAsync(AIDeploymentDeploymentStep step, DeploymentPlanResult result)
    {
        var deployments = await _profilesCatalog.GetAllAsync();

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
                { "ProviderName" , deployment.Source },
                { "ConnectionName", deployment.ConnectionName },
                { "Author", deployment.Author },
                { "OwnerId", deployment.OwnerId },
                { "CreatedUtc" , deployment.CreatedUtc },
                { "OwnerId" , deployment.OwnerId },
                { "Properties", deployment.Properties?.DeepClone() },
            };

            deploymentData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["deployments"] = deploymentData,
        });
    }
}
