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
/// with the same checks as the admin. Recipe steps of modules that build on Workforce use it too.
/// </summary>
public sealed class RecipeImport
{
    private readonly RecipeExecutionContext _context;
    private readonly string _stepName;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeImport"/> class.
    /// </summary>
    /// <param name="context">The recipe step being run.</param>
    /// <param name="stepName">The step's name, for the errors it reports.</param>
    public RecipeImport(RecipeExecutionContext context, string stepName)
    {
        _context = context;
        _stepName = stepName;
    }

    /// <summary>
    /// Reads the records of a section of the step, reporting those that can't be read.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="section">The section, such as <c>PlanningGroups</c>.</param>
    /// <returns>Each record with the JSON it was read from.</returns>
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
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="items">The records to look in.</param>
    /// <param name="name">Gets a record's name.</param>
    /// <param name="value">The name to find.</param>
    /// <param name="owner">What names it, such as <c>planning group</c>.</param>
    /// <param name="ownerName">The name of what names it.</param>
    /// <param name="kind">What it names, such as <c>a queue</c>.</param>
    /// <returns>The record, or <see langword="null"/>.</returns>
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

    /// <summary>
    /// Reads a list of texts, leaving out empty ones.
    /// </summary>
    /// <param name="node">The JSON of a record.</param>
    /// <param name="property">The property.</param>
    /// <returns>The texts.</returns>
    public static IReadOnlyList<string> Strings(JsonObject node, string property)
    {
        return (node[property] as JsonArray ?? [])
            .Select(item => item?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    /// <summary>
    /// Reads a text, or <see langword="null"/> when it is missing or empty.
    /// </summary>
    /// <param name="node">The JSON of a record.</param>
    /// <param name="property">The property.</param>
    /// <returns>The text.</returns>
    public static string String(JsonObject node, string property)
    {
        return node[property] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    /// <summary>
    /// Saves a record over the one with its id or name, or as a new one, after the same checks as the admin.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="manager">The record's manager.</param>
    /// <param name="incoming">The record read from the step.</param>
    /// <param name="existing">The record it updates, or <see langword="null"/> for a new one.</param>
    /// <param name="label">The record, for the errors, such as <c>planning group 'Sales'</c>.</param>
    /// <returns>The saved record, or <see langword="null"/> when it was refused.</returns>
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

    /// <summary>
    /// Finds the record a record of the step updates: the one with its id, else the one with its name.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="items">The records to look in.</param>
    /// <param name="incoming">The record read from the step.</param>
    /// <param name="name">Gets a record's name.</param>
    /// <returns>The record, or <see langword="null"/>.</returns>
    public static T Existing<T>(IEnumerable<T> items, T incoming, Func<T, string> name)
        where T : CatalogItem
    {
        return items.FirstOrDefault(item => !string.IsNullOrEmpty(incoming.ItemId) && item.ItemId == incoming.ItemId)
            ?? items.FirstOrDefault(item => WorkforceNames.Normalize(name(item)) == WorkforceNames.Normalize(name(incoming)));
    }
}
