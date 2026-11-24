using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ServiceLevels.Deployments;

/// <summary>
/// Exports every service level policy.
/// </summary>
public sealed class ServiceLevelPoliciesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ServiceLevelPoliciesDeploymentStep>("Configuration");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ServiceLevelPoliciesDeploymentStep>("Service level policies");

    /// <inheritdoc />
    public override string Name => ServiceLevelsConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
