using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>Campaign</c> recipe step.
/// </summary>
internal sealed class OmnichannelCampaignDeploymentSource : CatalogDeploymentSource<OmnichannelCampaignDeploymentStep, OmnichannelCampaign>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaigns.</param>
    public OmnichannelCampaignDeploymentSource(ICatalogManager<OmnichannelCampaign> manager)
        : base("Campaigns", manager)
    {
    }
}
