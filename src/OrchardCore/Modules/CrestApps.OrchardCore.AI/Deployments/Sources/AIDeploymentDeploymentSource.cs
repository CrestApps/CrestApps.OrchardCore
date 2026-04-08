using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIDeploymentDeploymentSource : DeploymentSourceBase<AIDeploymentDeploymentStep>
{
    private readonly INamedCatalog<AIDeployment> _deploymentCatalog;

    public AIDeploymentDeploymentSource(INamedCatalog<AIDeployment> deploymentCatalog)
    {
        _deploymentCatalog = deploymentCatalog;
    }

    protected override async Task ProcessAsync(AIDeploymentDeploymentStep step, DeploymentPlanResult result)
    {
        var deployments = await _deploymentCatalog.GetAllAsync();

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
                { "ItemId", deployment.ItemId },
                { "Name", deployment.Name },
                { "ModelName", deployment.ModelName },
                { "ClientName", deployment.Source },
                { "ProviderName" , deployment.Source },
                { "ConnectionName", deployment.ConnectionName },
                { "ConnectionNameAlias", deployment.ConnectionNameAlias },
                { "Type", deployment.Type.ToString() },
                { "IsDefault", deployment.IsDefault },
                { "Author", deployment.Author },
                { "OwnerId", deployment.OwnerId },
                { "CreatedUtc" , deployment.CreatedUtc },
                { "Properties", JsonSerializer.SerializeToNode(deployment.Properties) },
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
