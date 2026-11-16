using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Lti.Deployments;

/// <summary>
/// Exports Learning's settings of the external tools. The tools' OpenID applications (client id, redirect URIs, key set)
/// move with Orchard Core's OpenID steps, and secrets with Secrets.
/// </summary>
public sealed class LearningExternalToolsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningExternalToolsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningExternalToolsDeploymentStep>("External tools");

    /// <inheritdoc />
    public override string Name => LtiConstants.RecipeStepName;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
