using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel subject flow settings.
/// </summary>
public sealed class OmnichannelSubjectFlowSettingsDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectFlowSettingsDeploymentStep"/> class.
    /// </summary>
    public OmnichannelSubjectFlowSettingsDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.SubjectFlowSettings;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectFlowSettingsDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelSubjectFlowSettingsDeploymentStep(IStringLocalizer<OmnichannelSubjectFlowSettingsDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
