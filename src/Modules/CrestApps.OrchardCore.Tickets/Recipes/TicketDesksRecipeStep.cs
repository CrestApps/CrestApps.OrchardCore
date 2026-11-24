using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Creates or updates desks, matched by <c>ItemId</c> and then by name, so a recipe can set up the desk the feature
/// creates on its own (named Support) without knowing its id.
/// </summary>
internal sealed class TicketDesksRecipeStep : CatalogRecipeStep<TicketDesk>
{
    public const string EntriesName = "Desks";

    private readonly ICatalog<TicketDesk> _desks;

    public TicketDesksRecipeStep(ICatalogManager<TicketDesk> manager, ICatalog<TicketDesk> desks)
        : base(TicketsConstants.RecipeSteps.Desks, EntriesName, manager)
    {
        _desks = desks;
    }

    protected override async ValueTask<TicketDesk> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var name = data?[nameof(TicketDesk.Name)] is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null;

        return string.IsNullOrEmpty(name)
            ? null
            : (await _desks.GetAllAsync()).FirstOrDefault(desk => string.Equals(desk.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    protected override bool CanImport(System.Text.Json.Nodes.JsonObject token, global::OrchardCore.Recipes.Models.RecipeExecutionContext context)
        => TicketRecipeIds.CanImport(context.Name, token, context);
}
