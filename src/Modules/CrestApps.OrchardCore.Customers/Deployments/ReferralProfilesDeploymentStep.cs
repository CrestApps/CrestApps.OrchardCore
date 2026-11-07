using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Exports every customer's referral code, with the referrals it earned, as a <c>ReferralProfiles</c> recipe step.
/// </summary>
public sealed class ReferralProfilesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ReferralProfilesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ReferralProfilesDeploymentStep>("Referral codes");

    /// <inheritdoc/>
    public override string Name => CustomersConstants.Recipes.ReferralProfiles;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
