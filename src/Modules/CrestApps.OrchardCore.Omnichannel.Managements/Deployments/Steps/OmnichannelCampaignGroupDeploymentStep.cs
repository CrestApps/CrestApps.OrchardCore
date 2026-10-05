using OrchardCore.Deployment;
using OrchardCore.Localization;

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
        Category = LocalizationSource.Create<OmnichannelCampaignGroupDeploymentStep>("Omnichannel");
    }
}
