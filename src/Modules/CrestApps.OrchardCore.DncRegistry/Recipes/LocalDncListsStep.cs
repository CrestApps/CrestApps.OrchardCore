using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.DncRegistry.Recipes;

/// <summary>
/// Adds local do-not-call lists from a <c>LocalDncLists</c> recipe step. Each list is queued for import the way an
/// uploaded file is, so its numbers are checked, normalized and stored by the same background import. A list whose name
/// and country already exist is left as it is, so running a recipe twice adds nothing.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "LocalDncLists", "Lists": [ { "Name": "Opted out", "CountryCode": "US", "PhoneNumbers": [ "+12025550101" ] } ] }
/// </code>
/// </remarks>
internal sealed class LocalDncListsStep : NamedRecipeStepHandler
{
    public const string StepKey = "LocalDncLists";

    public const string EntriesName = "Lists";

    private readonly ILocalDncListManager _manager;

    internal readonly IStringLocalizer S;

    public LocalDncListsStep(ILocalDncListManager manager, IStringLocalizer<LocalDncListsStep> stringLocalizer)
        : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var node in (context.Step[EntriesName] as JsonArray ?? []).OfType<JsonObject>())
        {
            var name = Text(node, "Name")?.Trim();
            var countryCode = Text(node, "CountryCode")?.Trim().ToUpperInvariant();
            var numbers = (node["PhoneNumbers"] as JsonArray ?? [])
                .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null)
                .Where(number => !string.IsNullOrEmpty(number))
                .ToArray();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(countryCode))
            {
                context.Errors.Add(S["A local DNC list needs a name and a country code."]);

                continue;
            }

            if (numbers.Length == 0)
            {
                context.Errors.Add(S["The local DNC list '{0}' has no phone numbers.", name]);

                continue;
            }

            var existing = await _manager.GetListsAsync(countryCode);

            if (existing.Any(list => string.Equals(list.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // One number per row, the shape of an uploaded file, so the list goes through the same import.
            var csv = new StringBuilder("PhoneNumber");

            foreach (var number in numbers)
            {
                csv.Append('\n').Append(number.Replace("\"", string.Empty, StringComparison.Ordinal));
            }

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

            await _manager.QueueImportAsync(name, countryCode, $"{name}.csv", stream);
        }
    }

    private static string Text(JsonObject node, string name)
        => node[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
