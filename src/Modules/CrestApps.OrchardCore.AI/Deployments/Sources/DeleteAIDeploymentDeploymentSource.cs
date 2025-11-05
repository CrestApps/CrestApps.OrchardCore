using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class DeleteAIDeploymentDeploymentSource : DeploymentSourceBase<DeleteAIDeploymentDeploymentStep>
{
    protected override Task ProcessAsync(DeleteAIDeploymentDeploymentStep step, DeploymentPlanResult result)
    {
        var payload = new JsonObject
        {
            ["name"] = step.Name,
        };

        if (!step.IncludeAll)
        {
            payload["DeploymentNames"] = new JsonArray(step.DeploymentNames?.Select(n => (JsonNode)n)?.ToArray() ?? []);
        }
        else
        {
            payload["IncludeAll"] = true;
        }

        result.Steps.Add(payload);

        return Task.CompletedTask;
    }
}
