using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Exports the store's own tax registrations, with their jurisdictions, numbers and thresholds, as a <c>MerchantTaxRegistration</c> recipe step.
/// </summary>
public sealed class MerchantTaxRegistrationDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MerchantTaxRegistrationDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MerchantTaxRegistrationDeploymentStep>("Tax registrations");

    /// <inheritdoc/>
    public override string Name => TaxationDeploymentSteps.MerchantTaxRegistration;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
