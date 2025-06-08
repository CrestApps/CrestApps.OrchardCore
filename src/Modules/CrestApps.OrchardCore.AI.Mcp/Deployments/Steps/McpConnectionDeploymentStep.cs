using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

internal sealed class McpConnectionDeploymentStep : DeploymentStep
{
    public McpConnectionDeploymentStep()
    {
        Name = McpConnectionStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] ConnectionIds { get; set; }
}
