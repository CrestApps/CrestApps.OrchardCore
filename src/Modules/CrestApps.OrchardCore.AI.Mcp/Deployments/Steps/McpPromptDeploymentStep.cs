using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

internal sealed class McpPromptDeploymentStep : DeploymentStep
{
    public McpPromptDeploymentStep()
    {
        Name = McpPromptStep.StepKey;
    }

    public bool IncludeAll { get; set; }

    public string[] PromptIds { get; set; }
}
