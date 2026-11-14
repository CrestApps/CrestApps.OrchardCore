using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every ticket status into a deployment plan's recipe.
/// </summary>
internal sealed class TicketStatusesDeploymentSource : CatalogDeploymentSource<TicketStatusesDeploymentStep, TicketStatus>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketStatusesDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketStatusesDeploymentSource(ICatalogManager<TicketStatus> manager)
        : base(TicketStatusesRecipeStep.EntriesName, manager)
    {
    }
}
