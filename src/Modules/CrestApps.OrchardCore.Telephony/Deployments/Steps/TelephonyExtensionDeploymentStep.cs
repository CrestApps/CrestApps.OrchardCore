using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telephony.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports telephony extensions.
/// </summary>
public sealed class TelephonyExtensionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TelephonyExtensionDeploymentStep>("Telephony");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TelephonyExtensionDeploymentStep>("Telephony Extensions");

    /// <inheritdoc />
    public override string Name => TelephonyDeploymentSteps.Extension;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
