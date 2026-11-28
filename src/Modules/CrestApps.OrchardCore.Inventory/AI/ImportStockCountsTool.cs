using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// Sets the units on hand of many stock items at once, as the CSV import of Commerce › Inventory does
/// (<see cref="StockCountImporter.ImportAsync"/>): each item whose count differs gets a Counted movement with the reason,
/// and it is all or nothing. The rows are checked first with the importer's own rules (a whole count of zero or more, an
/// item found by product and variant or by SKU, no item counted twice), so a wrong row changes nothing; at most
/// <see cref="MaxRows"/> rows. The counts are committed at once. It needs <see cref="InventoryPermissions.ManageInventory"/>.
/// </summary>
internal sealed class ImportStockCountsTool : InventoryAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "importStockCounts";

    /// <summary>
    /// The most rows one call takes, as the CSV import.
    /// </summary>
    public const int MaxRows = 10_000;

    private const int MaxProblemsShown = 20;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "rows": {
          "type": "array",
          "description": "The counts, one per product or variant.",
          "items": {
            "type": "object",
            "properties": {
              "sku": { "type": "string", "description": "The SKU of the product or variant, ignoring case. A SKU that more than one counted item shares finds none." },
              "contentItemId": { "type": "string", "description": "The product's content item id, used before the SKU when given." },
              "variantId": { "type": "string", "description": "The variant's id, with contentItemId." },
              "count": { "type": "integer", "description": "The units on hand counted, zero or more." }
            },
            "required": ["count"],
            "additionalProperties": false
          }
        },
        "reason": {
          "type": "string",
          "description": "Why the counts change, such as 'Year-end stocktake'. It is kept with every movement."
        }
      },
      "required": ["rows", "reason"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets the units on hand of many items at once from rows of SKU (or content item id and variant id) and counted units. It is all or nothing: if any row is wrong nothing changes and the problems come back by row; at most 10,000 rows.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => InventoryPermissions.ManageInventory;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var reason = ReadString(arguments, "reason");

        if (reason is null)
        {
            return "Nothing was changed: say why the counts change. The reason is kept with every movement.";
        }

        if (ReadJson(arguments, "rows") is not { ValueKind: JsonValueKind.Array } rowsElement || rowsElement.GetArrayLength() == 0)
        {
            return "Nothing was changed: give the counts as a list of rows, each with a sku (or contentItemId and variantId) and a count.";
        }

        if (rowsElement.GetArrayLength() > MaxRows)
        {
            return $"Nothing was changed: at most {MaxRows} rows are counted at once. Split the counts and send each part.";
        }

        var rows = new List<StockCountRow>();
        var line = 0;

        foreach (var row in rowsElement.EnumerateArray())
        {
            line++;

            if (row.ValueKind != JsonValueKind.Object)
            {
                return $"Nothing was changed: row {line} is not an object.";
            }

            rows.Add(new StockCountRow
            {
                Line = line,
                Sku = row.TryGetProperty("sku", out var sku) ? ToText(sku) : null,
                ContentItemId = row.TryGetProperty("contentItemId", out var contentItemId) ? ToText(contentItemId) : null,
                VariantId = row.TryGetProperty("variantId", out var variantId) ? ToText(variantId) : null,
                OnHand = row.TryGetProperty("count", out var count) ? ToInt(count) : null,
            });
        }

        var importer = services.GetRequiredService<StockCountImporter>();
        var problems = await CheckAsync(importer, rows, cancellationToken);

        if (problems.Count > 0)
        {
            return Refuse(problems);
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        var session = services.GetRequiredService<YesSqlSession>();
        var result = await importer.ImportAsync(rows, reason, StockActor.ForUser(caller), cancellationToken);

        if (result.Problems.Count > 0)
        {
            // All or nothing: a count the inventory refused cancels the others.
            await session.CancelAsync();
            MarkSessionClosed(services);

            return Refuse(result.Problems.Select(problem => Describe(problem.Line, problem.Kind, problem.Item)).ToList());
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();
            MarkSessionClosed(services);

            return "Nothing was changed: someone else changed some of this stock while it was counted. Send the counts again in your next message.";
        }

        return Json(new
        {
            rows = rows.Count,
            counted = result.Counted,
            unchanged = result.Unchanged,
            reason,
            message = $"Counted {result.Counted} items. {result.Unchanged} already had that count.",
        });
    }

    // The importer's own rules, run before anything changes: a whole count of zero or more, an item found, and no
    // item counted twice.
    private static async Task<List<string>> CheckAsync(StockCountImporter importer, IReadOnlyList<StockCountRow> rows, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.OnHand is not >= 0)
            {
                problems.Add(Describe(row.Line, StockCountProblemKind.InvalidCount, row.Sku ?? row.ContentItemId));

                continue;
            }

            if (row.Sku is null && row.ContentItemId is null)
            {
                problems.Add($"Row {row.Line}: give a sku, or a contentItemId (with variantId for a variant).");

                continue;
            }

            var item = await importer.FindAsync(row, cancellationToken);

            if (item is null)
            {
                problems.Add(Describe(row.Line, StockCountProblemKind.NotFound, row.Sku ?? row.ContentItemId));

                continue;
            }

            if (!seen.Add(item.ItemId))
            {
                problems.Add(Describe(row.Line, StockCountProblemKind.Duplicate, item.Sku ?? item.Title));
            }
        }

        return problems;
    }

    private static string Describe(int line, StockCountProblemKind kind, string item)
        => kind switch
        {
            StockCountProblemKind.InvalidCount => $"Row {line}: the count of '{item}' must be a whole number of zero or more.",
            StockCountProblemKind.Duplicate => $"Row {line}: '{item}' is counted on an earlier row too.",
            StockCountProblemKind.TooManyRows => $"There are more than {MaxRows} rows. Split them and send each part.",
            _ => $"Row {line}: no product or variant is counted with '{item}', or more than one shares that SKU.",
        };

    private static string Refuse(List<string> problems)
        => Json(new
        {
            changed = false,
            problems = problems.Take(MaxProblemsShown).ToArray(),
            problemCount = problems.Count,
            truncated = problems.Count > MaxProblemsShown,
            message = "Nothing was changed. Fix the rows and send the counts again.",
        });
}
