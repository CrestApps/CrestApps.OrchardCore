using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Telnyx SMS provider settings, without any of the secrets they hold.
/// </summary>
public sealed class TelnyxSmsSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TelnyxSmsSettingsDeploymentStep>("Telephony");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TelnyxSmsSettingsDeploymentStep>("Telnyx SMS Settings");

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSmsSettingsDeploymentStep"/> class.
    /// </summary>
    public TelnyxSmsSettingsDeploymentStep()
    {
        Name = TelnyxDeploymentSteps.SmsSettings;
        Category = _category;
        Title = _title;
    }
}
