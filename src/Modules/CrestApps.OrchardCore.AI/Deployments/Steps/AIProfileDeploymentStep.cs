using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI profiles.
/// </summary>
public sealed class AIProfileDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIProfileDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIProfileDeploymentStep>("AI Profiles");

    /// <inheritdoc />
    public override string Name => AIProfileStep.StepKey;

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
    /// Gets or sets the profile names.
    /// </summary>
    public string[] ProfileNames { get; set; }
}
