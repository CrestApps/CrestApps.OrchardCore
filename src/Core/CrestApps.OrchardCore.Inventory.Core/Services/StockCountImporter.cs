using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Applies counts from a file or a recipe: each item found gets a <see cref="StockMovementKind.Counted"/> movement for
/// the difference. Like every stock change, the counts are made in the caller's unit of work, and the caller commits
/// them all or none.
/// </summary>
public sealed class StockCountImporter
{
    private readonly IStockItemStore _items;
    private readonly IInventoryService _inventory;

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

    public async Task<StockCountImportResult> ImportAsync(IEnumerable<StockCountRow> rows, string reason, StockActor actor, CancellationToken cancellationToken = default)
    {
        var result = new StockCountImportResult();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows ?? [])
        {
            if (row.TooMany)
            {
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.TooManyRows, null));

                break;
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
                result.Problems.Add(new StockCountProblem(row.Line, StockCountProblemKind.InvalidCount, item.Sku ?? item.Title));
            }
        }

        return result;
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
