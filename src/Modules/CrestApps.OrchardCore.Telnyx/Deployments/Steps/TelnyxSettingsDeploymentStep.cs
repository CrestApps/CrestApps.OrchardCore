using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Telnyx voice provider settings, without any of the secrets they hold.
/// </summary>
public sealed class TelnyxSettingsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSettingsDeploymentStep"/> class.
    /// </summary>
    public TelnyxSettingsDeploymentStep()
    {
        Name = TelnyxDeploymentSteps.Settings;
        Category = LocalizationSource.Create<TelnyxSettingsDeploymentStep>("Telephony");
    }
}
