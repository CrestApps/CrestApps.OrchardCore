using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Checks.Deployments;

/// <summary>
/// Exports the check register, for a site that takes over the checks of this one; imported checks are history.
/// </summary>
public sealed class CheckRegisterDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CheckRegisterDeploymentStep>("Finance");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CheckRegisterDeploymentStep>("Check register");

    /// <inheritdoc />
    public override string Name => ChecksConstants.RegisterRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
