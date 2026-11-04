using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Exports the activity codes and the team hierarchy, and optionally the workers.
/// </summary>
public sealed class WorkforceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<WorkforceDeploymentStep>("Workforce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<WorkforceDeploymentStep>("Workforce teams and workers");

    /// <inheritdoc />
    public override string Name => WorkforceConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the workers are exported too.
    /// </summary>
    public bool IncludeWorkers { get; set; }
}
