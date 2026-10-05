using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports MCP connections.
/// </summary>
public sealed class McpConnectionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionDeploymentStep"/> class.
    /// </summary>
    public McpConnectionDeploymentStep()
    {
        Name = McpConnectionStep.StepKey;
        Category = LocalizationSource.Create<McpConnectionDeploymentStep>("Artificial Intelligence");
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the connection ids.
    /// </summary>
    public string[] ConnectionIds { get; set; }
}
