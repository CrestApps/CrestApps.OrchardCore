using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Inventory.Recipes;

/// <summary>
/// The "Stock" step: sets how many units of each product or variant are on hand, as a count.
/// </summary>
/// <example>
/// <code>
/// { "name": "Stock", "Items": [ { "Sku": "MUG", "OnHand": 24 }, { "ContentItemId": "…", "VariantId": "…", "OnHand": 3 } ] }
/// </code>
/// </example>
internal sealed class StockStep : NamedRecipeStepHandler
{
    private readonly StockCountImporter _importer;
    private readonly InventorySyncService _sync;

    internal readonly IStringLocalizer S;

    public StockStep(
        StockCountImporter importer,
        InventorySyncService sync,
        IStringLocalizer<StockStep> stringLocalizer)
        : base(InventoryConstants.StockRecipeStep)
    {
        _importer = importer;
        _sync = sync;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var rows = new List<StockCountRow>();
        var position = 0;

        foreach (var token in (context.Step["Items"] as JsonArray ?? []).OfType<JsonObject>())
        {
            position++;

            rows.Add(new StockCountRow
            {
                Line = position,
                Sku = Text(token, "Sku"),
                ContentItemId = Text(token, "ContentItemId"),
                VariantId = Text(token, "VariantId"),
                OnHand = token["OnHand"] is JsonValue value && value.TryGetValue<int>(out var count) ? count : null,
            });
        }

        // Products imported by an earlier step of the same recipe may not be counted yet.
        var missing = new List<string>();

        foreach (var row in rows.Where(row => !string.IsNullOrEmpty(row.ContentItemId)))
        {
            if (await _importer.FindAsync(row) is null)
            {
                missing.Add(row.ContentItemId);
            }
        }

        if (missing.Count > 0)
        {
            await _sync.SyncAsync(missing.Distinct(StringComparer.Ordinal));
        }

        var result = await _importer.ImportAsync(rows, S["Set by a recipe."], actor: null);

        foreach (var problem in result.Problems)
        {
            context.Errors.Add(problem.Kind switch
            {
                StockCountProblemKind.InvalidCount => S["Stock item {0}: OnHand must be a whole number of zero or more.", problem.Line],
                StockCountProblemKind.Duplicate => S["Stock item {0}: {1} is counted more than once.", problem.Line, problem.Item],
                _ => S["Stock item {0}: no product or variant is counted with the SKU or id '{1}'.", problem.Line, problem.Item],
            });
        }
    }

    private static string Text(JsonObject token, string name)
        => token[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}
