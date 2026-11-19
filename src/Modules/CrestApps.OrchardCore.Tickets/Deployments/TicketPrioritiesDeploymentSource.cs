using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Recipes;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Writes every ticket priority into a deployment plan's recipe.
/// </summary>
internal sealed class TicketPrioritiesDeploymentSource : CatalogDeploymentSource<TicketPrioritiesDeploymentStep, TicketPriority>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketPrioritiesDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketPrioritiesDeploymentSource(ICatalogManager<TicketPriority> manager)
        : base(TicketPrioritiesRecipeStep.EntriesName, manager)
    {
    }
}
