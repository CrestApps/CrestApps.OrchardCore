using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Telnyx SMS provider settings, without any of the secrets they hold.
/// </summary>
public sealed class TelnyxSmsSettingsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsSettingsDeploymentStep"/> class.
    /// </summary>
    public TelnyxSmsSettingsDeploymentStep()
    {
        Name = TelnyxDeploymentSteps.SmsSettings;
        Category = LocalizationSource.Create<TelnyxSmsSettingsDeploymentStep>("Telephony");
    }
}
