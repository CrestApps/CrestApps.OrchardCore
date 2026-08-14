using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelDispositionDeploymentStep(IStringLocalizer<OmnichannelDispositionDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
