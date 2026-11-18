using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every desk into a deployment plan's recipe.
/// </summary>
internal sealed class TicketDesksDeploymentSource : CatalogDeploymentSource<TicketDesksDeploymentStep, TicketDesk>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketDesksDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketDesksDeploymentSource(ICatalogManager<TicketDesk> manager)
        : base(TicketDesksRecipeStep.EntriesName, manager)
    {
    }
}
