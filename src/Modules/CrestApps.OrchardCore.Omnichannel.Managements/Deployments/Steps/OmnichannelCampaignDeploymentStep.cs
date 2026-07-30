using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel campaigns.
/// </summary>
public sealed class OmnichannelCampaignDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignDeploymentStep"/> class.
    /// </summary>
    public OmnichannelCampaignDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Campaign;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelCampaignDeploymentStep(IStringLocalizer<OmnichannelCampaignDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
