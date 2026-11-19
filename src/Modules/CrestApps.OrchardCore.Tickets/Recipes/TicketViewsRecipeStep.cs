using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates ticket views, matched by id or name.
/// </summary>
internal sealed class TicketViewsRecipeStep : CatalogRecipeStep<TicketView>
{
    /// <summary>
    /// The name of the step's list of records.
    /// </summary>
    public const string EntriesName = "Views";

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketViewsRecipeStep"/> class.
    /// </summary>
    /// <param name="manager">The records.</param>
    public TicketViewsRecipeStep(ICatalogManager<TicketView> manager)
        : base(TicketsConstants.RecipeSteps.Views, EntriesName, manager)
    {
    }

    /// <inheritdoc/>
    protected override bool CanImport(System.Text.Json.Nodes.JsonObject token, global::OrchardCore.Recipes.Models.RecipeExecutionContext context)
        => TicketRecipeIds.CanImport(context.Name, token, context);
}
