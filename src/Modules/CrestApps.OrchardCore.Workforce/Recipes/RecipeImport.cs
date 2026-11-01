using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Core;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// What the Workforce settings recipe steps share: reading a section, finding records by id or name, and saving one
/// with the same checks as the admin.
/// </summary>
internal sealed class RecipeImport
{
    private readonly RecipeExecutionContext _context;
    private readonly string _stepName;

    public RecipeImport(RecipeExecutionContext context, string stepName)
    {
        _context = context;
        _stepName = stepName;
    }

    public IEnumerable<(JsonObject Node, T Item)> Read<T>(string section)
        where T : class
    {
        foreach (var node in (_context.Step[section] as JsonArray ?? []).OfType<JsonObject>())
        {
            T item = null;

            try
            {
                item = node.Deserialize<T>(WorkforceRecipeStep.JsonOptions);
            }
            catch (JsonException ex)
            {
                _context.Errors.Add($"An entry of {section} in the {_stepName} step can't be read: {ex.Message}");
            }

            if (item is not null)
            {
                yield return (node, item);
            }
        }
    }

    /// <summary>
    /// Finds a record by name, or reports the name as missing and returns <see langword="null"/>.
    /// </summary>
    public T Named<T>(IEnumerable<T> items, Func<T, string> name, string value, string owner, string ownerName, string kind)
        where T : class
    {
        var found = items.FirstOrDefault(item => WorkforceNames.Normalize(name(item)) == WorkforceNames.Normalize(value));

        if (found is null)
        {
            _context.Errors.Add($"The {owner} '{ownerName}' in the {_stepName} step names {kind} that doesn't exist: '{value}'.");
        }

        return found;
    }

    public static IReadOnlyList<string> Strings(JsonObject node, string property)
    {
        return (node[property] as JsonArray ?? [])
            .Select(item => item?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    public static string String(JsonObject node, string property)
    {
        return node[property] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    /// <summary>
    /// Saves a record over the one with its id or name, or as a new one, after the same checks as the admin.
    /// </summary>
    public async Task<T> SaveAsync<T>(ICatalogManager<T> manager, T incoming, T existing, string label)
        where T : CatalogItem
    {
        var target = existing ?? await manager.NewAsync();
        var id = existing?.ItemId ?? (string.IsNullOrEmpty(incoming.ItemId) ? target.ItemId : incoming.ItemId);

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && property.Name is not ("CreatedUtc" or "ModifiedUtc"))
            {
                property.SetValue(target, property.GetValue(incoming));
            }
        }

        target.ItemId = id;

        var validation = await manager.ValidateAsync(target);

        if (!validation.Succeeded)
        {
            _context.Errors.Add($"The {label} in the {_stepName} step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

            return null;
        }

        if (existing is null)
        {
            await manager.CreateAsync(target);
        }
        else
        {
            await manager.UpdateAsync(target);
        }

        return target;
    }

    public static T Existing<T>(IEnumerable<T> items, T incoming, Func<T, string> name)
        where T : CatalogItem
    {
        return items.FirstOrDefault(item => !string.IsNullOrEmpty(incoming.ItemId) && item.ItemId == incoming.ItemId)
            ?? items.FirstOrDefault(item => WorkforceNames.Normalize(name(item)) == WorkforceNames.Normalize(name(incoming)));
    }
}
