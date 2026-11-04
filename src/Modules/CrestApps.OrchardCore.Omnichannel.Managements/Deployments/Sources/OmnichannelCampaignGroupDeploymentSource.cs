using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>CampaignGroup</c> recipe step.
/// </summary>
internal sealed class OmnichannelCampaignGroupDeploymentSource : CatalogDeploymentSource<OmnichannelCampaignGroupDeploymentStep, OmnichannelCampaignGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaign groups.</param>
    public OmnichannelCampaignGroupDeploymentSource(ICatalogManager<OmnichannelCampaignGroup> manager)
        : base("CampaignGroups", manager)
    {
    }
}
