using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel dispositions.
/// </summary>
public sealed class OmnichannelDispositionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionDeploymentStep"/> class.
    /// </summary>
    public OmnichannelDispositionDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Disposition;
        Category = LocalizationSource.Create<OmnichannelDispositionDeploymentStep>("Omnichannel");
    }
}
