using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Exports customers' tax exemption certificates, with their ids so customers keep pointing at them, as an <c>ExemptionCertificate</c> recipe step.
/// </summary>
public sealed class ExemptionCertificateDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ExemptionCertificateDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ExemptionCertificateDeploymentStep>("Exemption certificates");

    /// <inheritdoc/>
    public override string Name => TaxationDeploymentSteps.ExemptionCertificate;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
