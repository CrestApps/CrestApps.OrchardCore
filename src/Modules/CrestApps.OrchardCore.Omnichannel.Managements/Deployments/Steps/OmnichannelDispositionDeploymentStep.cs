using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel dispositions.
/// </summary>
public sealed class OmnichannelDispositionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelDispositionDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelDispositionDeploymentStep>("Omnichannel Dispositions");

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionDeploymentStep"/> class.
    /// </summary>
    public OmnichannelDispositionDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Disposition;
        Category = _category;
        Title = _title;
    }
}
