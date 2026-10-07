using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel campaign groups.
/// </summary>
public sealed class OmnichannelCampaignGroupDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelCampaignGroupDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelCampaignGroupDeploymentStep>("Omnichannel Campaign Groups");

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDeploymentStep"/> class.
    /// </summary>
    public OmnichannelCampaignGroupDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.CampaignGroup;
        Category = _category;
        Title = _title;
    }
}
