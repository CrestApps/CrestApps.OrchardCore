using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel campaign groups.
/// </summary>
public sealed class OmnichannelCampaignGroupDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDeploymentStep"/> class.
    /// </summary>
    public OmnichannelCampaignGroupDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.CampaignGroup;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignGroupDeploymentStep(IStringLocalizer<OmnichannelCampaignGroupDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
