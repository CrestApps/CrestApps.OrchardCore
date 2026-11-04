using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Core.Recipes;

/// <summary>
/// Exports and imports the entries of a catalog as one section of a recipe step, for steps that carry several kinds of
/// records. Entries keep their identifiers, so records that refer to each other by id stay linked on the site the
/// step is run on.
/// </summary>
public static class CatalogRecipeSections
{
    /// <summary>
    /// Writes entries as a section's array, in the shape <see cref="ImportAsync{T}"/> reads.
    /// </summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <param name="entries">The entries.</param>
    /// <returns>The array.</returns>
    public static JsonArray Export<T>(IEnumerable<T> entries)
        where T : CatalogItem
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new JsonArray([.. entries.Select(entry => (JsonNode)CatalogDeploymentSerializer.Export(entry))]);
    }

    /// <summary>
    /// Creates or updates, by identifier, the entries of a section of a recipe step, with the same checks as the
    /// admin. An entry that is refused is reported as a recipe error and the others are still imported.
    /// </summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <param name="context">The recipe step being run.</param>
    /// <param name="section">The section, such as <c>Types</c>.</param>
    /// <param name="manager">The manager the entries are saved through.</param>
    /// <param name="label">What an entry is, for the errors, such as <c>work order type</c>.</param>
    /// <param name="prepare">Changes an entry before it is checked, such as to resolve names into ids.</param>
    /// <returns>The entries saved.</returns>
    public static async Task<IReadOnlyList<T>> ImportAsync<T>(RecipeExecutionContext context, string section, ICatalogManager<T> manager, string label, Func<T, JsonObject, Task> prepare = null)
        where T : CatalogItem
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manager);

        var saved = new List<T>();

        foreach (var node in Section(context.Step, section))
        {
            var id = node[nameof(CatalogItem.ItemId)] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var entry = string.IsNullOrEmpty(id) ? null : await manager.FindByIdAsync(id);
            var isNew = entry is null;

            if (isNew)
            {
                entry = await manager.NewAsync();

                if (!string.IsNullOrEmpty(id) && UniqueId.IsValid(id))
                {
                    entry.ItemId = id;
                }
            }

            var itemId = entry.ItemId;

            CatalogDeploymentSerializer.Populate(entry, node);
            entry.ItemId = itemId;

            if (prepare is not null)
            {
                await prepare(entry, node);
            }

            var validation = await manager.ValidateAsync(entry);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"A {label} in the {context.Name} step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (isNew)
            {
                await manager.CreateAsync(entry);
            }
            else
            {
                await manager.UpdateAsync(entry);
            }

            saved.Add(entry);
        }

        return saved;
    }

    /// <summary>
    /// Reads a section's entries, matching its name without regard to case.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="section">The section.</param>
    /// <returns>The entries.</returns>
    public static IEnumerable<JsonObject> Section(JsonObject step, string section)
    {
        if (step is null)
        {
            return [];
        }

        if (!step.TryGetPropertyValue(section, out var entries))
        {
            entries = step.FirstOrDefault(property => string.Equals(property.Key, section, StringComparison.OrdinalIgnoreCase)).Value;
        }

        return (entries as JsonArray ?? []).OfType<JsonObject>();
    }
}
