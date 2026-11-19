using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates ticket boards, matched by id. A personal board names its owner by user name (OwnerUserName).
/// </summary>
internal sealed class TicketBoardsRecipeStep : CatalogRecipeStep<TicketBoard>
{
    /// <summary>
    /// The name of the step's list of records.
    /// </summary>
    public const string EntriesName = "Boards";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketBoardsRecipeStep"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketBoardsRecipeStep(ICatalogManager<TicketBoard> manager)
        : base(TicketsConstants.RecipeSteps.Boards, EntriesName, manager)
    {
    }

    /// <inheritdoc/>
    protected override bool CanImport(System.Text.Json.Nodes.JsonObject token, global::OrchardCore.Recipes.Models.RecipeExecutionContext context)
        => TicketRecipeIds.CanImport(context.Name, token, context);
}
