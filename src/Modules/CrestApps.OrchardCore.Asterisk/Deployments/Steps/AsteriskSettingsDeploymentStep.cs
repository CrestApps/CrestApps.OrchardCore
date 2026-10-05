using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Asterisk.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Asterisk provider settings, without any of the secrets they hold.
/// </summary>
public sealed class AsteriskSettingsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsDeploymentStep"/> class.
    /// </summary>
    public AsteriskSettingsDeploymentStep()
    {
        Name = AsteriskDeploymentSteps.Settings;
        Category = LocalizationSource.Create<AsteriskSettingsDeploymentStep>("Telephony");
    }
}
