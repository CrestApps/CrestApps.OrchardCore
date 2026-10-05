using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the lead statuses.
/// </summary>
public sealed class LeadStatusDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDeploymentStep"/> class.
    /// </summary>
    public LeadStatusDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.LeadStatus;
        Category = LocalizationSource.Create<LeadStatusDeploymentStep>("Omnichannel");
    }
}
