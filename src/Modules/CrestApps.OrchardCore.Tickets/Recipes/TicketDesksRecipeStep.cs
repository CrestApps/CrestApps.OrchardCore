using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates desks, matched by id or name.
/// </summary>
internal sealed class TicketDesksRecipeStep : CatalogRecipeStep<TicketDesk>
{
    /// <summary>
    /// The name of the step's list of records.
    /// </summary>
    public const string EntriesName = "Desks";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketDesksRecipeStep"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketDesksRecipeStep(ICatalogManager<TicketDesk> manager)
        : base(TicketsConstants.RecipeSteps.Desks, EntriesName, manager)
    {
    }
}
