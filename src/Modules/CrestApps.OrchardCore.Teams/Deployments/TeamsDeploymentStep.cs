using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Teams.Deployments;

/// <summary>
/// Exports every team, with its parent by name.
/// </summary>
public sealed class TeamsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TeamsDeploymentStep>("Organization");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TeamsDeploymentStep>("Teams");

    /// <inheritdoc />
    public override string Name => TeamsConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
