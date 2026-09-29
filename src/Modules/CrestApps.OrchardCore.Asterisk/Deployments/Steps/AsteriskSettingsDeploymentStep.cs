using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskSettingsDeploymentStep(IStringLocalizer<AsteriskSettingsDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Telephony"];
    }
}
