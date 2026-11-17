using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Deployments;

/// <summary>
/// Exports disbursements, for a site that takes over the payments of this one; imported disbursements are history.
/// </summary>
public sealed class DisbursementsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<DisbursementsDeploymentStep>("Finance");
    private static readonly LocalizationSource _title = LocalizationSource.Create<DisbursementsDeploymentStep>("Disbursements");

    /// <inheritdoc />
    public override string Name => DisbursementsConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
