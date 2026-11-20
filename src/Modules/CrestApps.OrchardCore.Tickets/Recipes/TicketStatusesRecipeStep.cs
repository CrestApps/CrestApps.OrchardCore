using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates ticket statuses, matched by id or name.
/// </summary>
internal sealed class TicketStatusesRecipeStep : CatalogRecipeStep<TicketStatus>
{
    /// <summary>
    /// The name of the step's list of records.
    /// </summary>
    public const string EntriesName = "Statuses";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketStatusesRecipeStep"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketStatusesRecipeStep(ICatalogManager<TicketStatus> manager)
        : base(TicketsConstants.RecipeSteps.Statuses, EntriesName, manager)
    {
    }

    /// <inheritdoc/>
    protected override bool CanImport(System.Text.Json.Nodes.JsonObject token, global::OrchardCore.Recipes.Models.RecipeExecutionContext context)
        => TicketRecipeIds.CanImport(context.Name, token, context);
}
