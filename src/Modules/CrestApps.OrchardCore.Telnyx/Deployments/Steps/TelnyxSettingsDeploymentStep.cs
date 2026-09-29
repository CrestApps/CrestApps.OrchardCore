using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSettingsDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelnyxSettingsDeploymentStep(IStringLocalizer<TelnyxSettingsDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Telephony"];
    }
}
