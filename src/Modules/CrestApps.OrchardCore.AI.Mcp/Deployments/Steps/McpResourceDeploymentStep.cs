using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports MCP resources.
/// </summary>
public sealed class McpResourceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<McpResourceDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<McpResourceDeploymentStep>("MCP Resources");

    /// <summary>
    /// Initializes a new instance of the <see cref="McpResourceDeploymentStep"/> class.
    /// </summary>
    public McpResourceDeploymentStep()
    {
        Name = McpResourceStep.StepKey;
        Category = _category;
        Title = _title;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the resource ids.
    /// </summary>
    public string[] ResourceIds { get; set; }
}
