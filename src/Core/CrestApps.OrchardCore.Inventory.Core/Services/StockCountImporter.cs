using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Telemetry;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Applies counts from a file or a recipe: each item found gets a <see cref="StockMovementKind.Counted"/> movement for
/// the difference. Like every stock change, the counts are made in the caller's unit of work, and the caller commits
/// them all or none.
/// </summary>
/// <remarks>
/// Each import is measured on <see cref="InventoryTelemetry"/>: a span, its duration and its rows by what became of
/// them.
/// </remarks>
public sealed class StockCountImporter
{
    private readonly IStockItemStore _items;
    private readonly IInventoryService _inventory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StockCountImporter"/> class.
    /// </summary>
    /// <param name="items">The store of the stock items.</param>
    /// <param name="inventory">The inventory service the counts are set through.</param>
    public StockCountImporter(IStockItemStore items, IInventoryService inventory)
    {
        _items = items;
        _inventory = inventory;
    }

    /// <summary>
    /// Finds the item a row means: by its product and variant ids when they match an item, otherwise by its SKU.
    /// </summary>
    public async Task<StockItem> FindAsync(StockCountRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.IsNullOrEmpty(row.ContentItemId) &&
            await _items.FindAsync(row.ContentItemId, row.VariantId, cancellationToken: cancellationToken) is { Archived: false } item)
        {
            return item;
        }

        return string.IsNullOrEmpty(row.Sku) ? null : await _items.FindBySkuAsync(row.Sku, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Applies the counts of the rows, in the caller's unit of work.
    /// </summary>
    /// <param name="rows">The rows of the count.</param>
    /// <param name="reason">The reason recorded on each movement.</param>
    /// <param name="actor">Who made the count.</param>
    /// <param name="cancellationToken">The token that cancels the import.</param>
    /// <returns>How many items were counted, and the rows that were not applied.</returns>
    public async Task<StockCountImportResult> ImportAsync(IEnumerable<StockCountRow> rows, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        using var telemetry = TelemetryScope.Start(InventoryTelemetry.ActivitySource, InventoryTelemetry.StockCountSpanName, InventoryTelemetry.StockCountDuration);

        StockCountImportResult result;
        int rejected;
        int truncatedAtLine;

        try
        {
            (result, rejected, truncatedAtLine) = await ApplyAsync(rows, reason, actor, cancellationToken);
            telemetry.SetOutcome(truncatedAtLine > 0 ? InventoryTelemetry.OutcomeTruncated : CrestAppsOrchardCoreTelemetry.OutcomeOk);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }

        Record(result, rejected, telemetry);

        return result;
    }

    // Records the rows by what became of them.
    private static void Record(StockCountImportResult result, int rejected, TelemetryScope telemetry)
    {
        var notFound = 0;
        var invalidCount = 0;
        var duplicate = 0;

        foreach (var problem in result.Problems)
        {
            switch (problem.Kind)
            {
                case StockCountProblemKind.NotFound:
                    notFound++;
                    break;

                case StockCountProblemKind.InvalidCount:
                    invalidCount++;
                    break;

                case StockCountProblemKind.Duplicate:
                    duplicate++;
                    break;
            }
        }

        // A count the inventory refused is reported as an invalid count; it is told apart here.
        invalidCount = Math.Max(0, invalidCount - rejected);

        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowCounted, result.Counted);
        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowUnchanged, result.Unchanged);
        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowNotFound, notFound);
        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowInvalidCount, invalidCount);
        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowDuplicate, duplicate);
        InventoryTelemetry.RecordStockCountRows(InventoryTelemetry.RowRejected, rejected);

        var notApplied = notFound + invalidCount + duplicate + rejected;

        telemetry.SetSpanTag("crestapps.inventory.rows.counted", result.Counted);
        telemetry.SetSpanTag("crestapps.inventory.rows.unchanged", result.Unchanged);
        telemetry.SetSpanTag("crestapps.inventory.rows.not_applied", notApplied);
    }

    private async Task<(StockCountImportResult Result, int Rejected, int TruncatedAtLine)> ApplyAsync(IEnumerable<StockCountRow> rows, string reason, StockActor actor, CancellationToken cancellationToken)
    {
        var result = new StockCountImportResult();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rejected = 0;

        foreach (var row in rows ?? [])
        {
            if (row.TooMany)
            {
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.TooManyRows, null));

                return (result, rejected, row.Line);
            }

            if (row.OnHand is not { } count || count < 0)
            {
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.InvalidCount, row.Sku ?? row.ContentItemId));

                continue;
            }

            var item = await FindAsync(row, cancellationToken);

            if (item is null)
            {
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.NotFound, row.Sku ?? row.ContentItemId));

                continue;
            }

            // Two counts for one item would leave whichever came last; neither is applied twice, and the second is
            // pointed out.
            if (!seen.Add(item.ItemId))
            {
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.Duplicate, item.Sku ?? item.Title));

                continue;
            }

            if (item.OnHand == count)
            {
                result.Unchanged++;

                continue;
            }

            var change = await _inventory.SetCountAsync(item.ItemId, count, reason, actor, cancellationToken);

            if (change.Succeeded)
            {
                result.Counted++;
            }
            else
            {
                rejected++;
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.InvalidCount, item.Sku ?? item.Title));
            }
        }

        return (result, rejected, 0);
    }
}

public sealed class StockCountImportResult
{
    /// <summary>
    /// Gets or sets how many items had a new count.
    /// </summary>
    public int Counted { get; set; }

    /// <summary>
    /// Gets or sets how many items already had the count given.
    /// </summary>
    public int Unchanged { get; set; }

    public List<StockCountProblem> Problems { get; } = [];
}

/// <summary>
/// A row that wasn't applied, with the SKU or id it gave, if any.
/// </summary>
public sealed record StockCountProblem(int Line, StockCountProblemKind Kind, string Item);

public enum StockCountProblemKind
{
    /// <summary>
    /// No stock item has the ids or the SKU, or several share the SKU.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// The units on hand aren't a whole number of zero or more.
    /// </summary>
    InvalidCount = 1,

    /// <summary>
    /// An earlier row counted the same item.
    /// </summary>
    Duplicate = 2,

    /// <summary>
    /// The file has more rows than are read at once.
    /// </summary>
    TooManyRows = 3,
}
