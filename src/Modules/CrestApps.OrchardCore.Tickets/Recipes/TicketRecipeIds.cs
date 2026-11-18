using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tickets.Recipes;

/// <summary>
/// Refuses a ticket settings record whose recipe id isn't a valid unique id. Imported under a new id, the record would
/// silently lose every link other records make to it (a desk's policy, a view's status), so the recipe says which id to
/// fix instead.
/// </summary>
internal static class TicketRecipeIds
{
    /// <summary>
    /// Returns whether the record's id, if any, is a valid unique id, and adds an error naming it when it isn't.
    /// </summary>
    /// <param name="stepName">The recipe step.</param>
    /// <param name="token">The record's JSON.</param>
    /// <param name="context">The recipe execution context.</param>
    /// <returns>Whether the record can be imported.</returns>
    public static bool CanImport(string stepName, JsonObject token, RecipeExecutionContext context)
    {
        var id = token[nameof(CatalogItem.ItemId)] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        if (string.IsNullOrEmpty(id) || global::CrestApps.Core.UniqueId.IsValid(id))
        {
            return true;
        }

        var name = token["Name"] is JsonValue nameValue && nameValue.TryGetValue<string>(out var nameText) ? nameText : id;

        context.Errors.Add($"{stepName}: \"{name}\" has the id \"{id}\", which isn't a valid id (26 letters and digits, from IdGenerator.GenerateId), so the records that name it would lose their link. Use a valid id, or leave ItemId out.");

        return false;
    }
}
