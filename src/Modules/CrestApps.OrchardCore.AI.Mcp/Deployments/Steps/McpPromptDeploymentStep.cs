using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

internal sealed class McpPromptDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpPromptDeploymentStep"/> class.
    /// </summary>
    public McpPromptDeploymentStep()
    {
        Name = McpPromptStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the prompt ids.
    /// </summary>
    public string[] PromptIds { get; set; }
}
