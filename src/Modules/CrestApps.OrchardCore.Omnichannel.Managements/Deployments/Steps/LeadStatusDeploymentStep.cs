using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the lead statuses.
/// </summary>
public sealed class LeadStatusDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LeadStatusDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LeadStatusDeploymentStep>("Omnichannel Lead Statuses");

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDeploymentStep"/> class.
    /// </summary>
    public LeadStatusDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.LeadStatus;
        Category = _category;
        Title = _title;
    }
}
