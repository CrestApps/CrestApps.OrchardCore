using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<OmnichannelCampaignDeploymentStep>("Omnichannel");
    }
}
