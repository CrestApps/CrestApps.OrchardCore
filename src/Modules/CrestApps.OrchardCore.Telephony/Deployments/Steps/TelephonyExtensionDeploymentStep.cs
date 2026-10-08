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

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDeploymentStep"/> class.
    /// </summary>
    public TelephonyExtensionDeploymentStep()
    {
        Name = TelephonyDeploymentSteps.Extension;
        Category = _category;
        Title = _title;
    }
}
