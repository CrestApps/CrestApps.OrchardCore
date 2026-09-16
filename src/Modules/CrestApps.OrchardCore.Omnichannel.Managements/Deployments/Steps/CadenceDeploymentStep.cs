using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel re-engagement cadences.
/// </summary>
public sealed class CadenceDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceDeploymentStep"/> class.
    /// </summary>
    public CadenceDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Cadence;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CadenceDeploymentStep(IStringLocalizer<CadenceDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
