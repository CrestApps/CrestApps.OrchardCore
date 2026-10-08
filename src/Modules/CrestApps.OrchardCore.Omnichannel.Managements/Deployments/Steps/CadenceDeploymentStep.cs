using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel re-engagement cadences.
/// </summary>
public sealed class CadenceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CadenceDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CadenceDeploymentStep>("Omnichannel Cadences");

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceDeploymentStep"/> class.
    /// </summary>
    public CadenceDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Cadence;
        Category = _category;
        Title = _title;
    }
}
