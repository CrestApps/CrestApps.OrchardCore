using CrestApps.OrchardCore.AI.Mcp.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports MCP prompts.
/// </summary>
public sealed class McpPromptDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<McpPromptDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<McpPromptDeploymentStep>("MCP Prompts");

    /// <inheritdoc />
    public override string Name => McpPromptStep.StepKey;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the prompt ids.
    /// </summary>
    public string[] PromptIds { get; set; }
}
