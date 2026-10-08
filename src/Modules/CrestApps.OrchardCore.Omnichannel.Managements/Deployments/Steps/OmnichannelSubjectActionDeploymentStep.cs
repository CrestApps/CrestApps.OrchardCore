using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the actions a subject disposition triggers.
/// </summary>
public sealed class OmnichannelSubjectActionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelSubjectActionDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelSubjectActionDeploymentStep>("Omnichannel Subject Actions");

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectActionDeploymentStep"/> class.
    /// </summary>
    public OmnichannelSubjectActionDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.SubjectAction;
        Category = _category;
        Title = _title;
    }
}
