using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

internal sealed class McpResourceDeploymentStep : DeploymentStep
{
    public McpResourceDeploymentStep()
    {
        Name = McpResourceStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ResourceIds { get; set; }
}
