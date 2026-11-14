using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every ticket view into a deployment plan's recipe.
/// </summary>
internal sealed class TicketViewsDeploymentSource : CatalogDeploymentSource<TicketViewsDeploymentStep, TicketView>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewsDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketViewsDeploymentSource(ICatalogManager<TicketView> manager)
        : base(TicketViewsRecipeStep.EntriesName, manager)
    {
    }
}
