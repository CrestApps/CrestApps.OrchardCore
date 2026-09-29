using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadStatusDeploymentStep(IStringLocalizer<LeadStatusDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
