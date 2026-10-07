using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel campaigns.
/// </summary>
public sealed class OmnichannelCampaignDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelCampaignDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelCampaignDeploymentStep>("Omnichannel Campaigns");

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignDeploymentStep"/> class.
    /// </summary>
    public OmnichannelCampaignDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.Campaign;
        Category = _category;
        Title = _title;
    }
}
