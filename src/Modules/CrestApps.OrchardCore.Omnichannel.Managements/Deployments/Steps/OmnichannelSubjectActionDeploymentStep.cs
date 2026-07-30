using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the actions a subject disposition triggers.
/// </summary>
public sealed class OmnichannelSubjectActionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectActionDeploymentStep"/> class.
    /// </summary>
    public OmnichannelSubjectActionDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.SubjectAction;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectActionDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelSubjectActionDeploymentStep(IStringLocalizer<OmnichannelSubjectActionDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
