using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Telnyx.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the Telnyx voice provider settings, without any of the secrets they hold.
/// </summary>
public sealed class TelnyxSettingsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TelnyxSettingsDeploymentStep>("Telephony");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TelnyxSettingsDeploymentStep>("Telnyx Settings");

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSettingsDeploymentStep"/> class.
    /// </summary>
    public TelnyxSettingsDeploymentStep()
    {
        Name = TelnyxDeploymentSteps.Settings;
        Category = _category;
        Title = _title;
    }
}
