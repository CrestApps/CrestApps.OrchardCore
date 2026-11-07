using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel campaign groups carried by a recipe step, creating entries that do not exist and updating those
/// that do.
/// </summary>
internal sealed class OmnichannelCampaignGroupStep : CatalogRecipeStep<OmnichannelCampaignGroup>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaign groups.</param>
    public OmnichannelCampaignGroupStep(ICatalogManager<OmnichannelCampaignGroup> manager)
        : base(OmnichannelDeploymentSteps.CampaignGroup, "CampaignGroups", manager)
    {
    }
}
