using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Recipes;

/// <summary>
/// Imports Omnichannel campaigns carried by a recipe step, creating entries that do not exist and updating those that
/// do.
/// </summary>
internal sealed class OmnichannelCampaignStep : CatalogRecipeStep<OmnichannelCampaign>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaigns.</param>
    public OmnichannelCampaignStep(ICatalogManager<OmnichannelCampaign> manager)
        : base(OmnichannelDeploymentSteps.Campaign, "Campaigns", manager)
    {
    }
}
