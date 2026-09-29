using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyExtensionDeploymentStep(IStringLocalizer<TelephonyExtensionDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Telephony"];
    }
}
