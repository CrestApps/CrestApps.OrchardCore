using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates ticket priorities, matched by id or name.
/// </summary>
internal sealed class TicketPrioritiesRecipeStep : CatalogRecipeStep<TicketPriority>
{
    /// <summary>
    /// The name of the step's list of records.
    /// </summary>
    public const string EntriesName = "Priorities";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketPrioritiesRecipeStep"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketPrioritiesRecipeStep(ICatalogManager<TicketPriority> manager)
        : base(TicketsConstants.RecipeSteps.Priorities, EntriesName, manager)
    {
    }

    /// <inheritdoc/>
    protected override bool CanImport(System.Text.Json.Nodes.JsonObject token, global::OrchardCore.Recipes.Models.RecipeExecutionContext context)
        => TicketRecipeIds.CanImport(context.Name, token, context);
}
