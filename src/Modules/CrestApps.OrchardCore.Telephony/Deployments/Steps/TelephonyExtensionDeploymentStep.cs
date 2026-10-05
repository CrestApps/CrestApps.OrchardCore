using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telephony.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports telephony extensions.
/// </summary>
public sealed class TelephonyExtensionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDeploymentStep"/> class.
    /// </summary>
    public TelephonyExtensionDeploymentStep()
    {
        Name = TelephonyDeploymentSteps.Extension;
        Category = LocalizationSource.Create<TelephonyExtensionDeploymentStep>("Telephony");
    }
}
