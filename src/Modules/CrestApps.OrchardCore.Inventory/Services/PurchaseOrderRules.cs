using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// The rules of a purchase order as Commerce › Purchase orders and the purchase order AI tools apply them: its number
/// from a locked sequence, its details, its lines while it is a draft and their costs after, and the items low on stock
/// a new order can start with. Each rule returns what was wrong; the caller words it.
/// </summary>
internal static class PurchaseOrderRules
{
    /// <summary>
    /// The most lines a purchase order has.
    /// </summary>
    public const int MaxLines = 500;

    /// <summary>
    /// The most units of one line ordered or received at once.
    /// </summary>
    public const int MaxQuantity = 1_000_000;

    private const string NumberSeries = "Inventory.PurchaseOrder";
    private const int LowStockScanPageSize = 500;
    private const int LowStockScanMaxItems = 5_000;

    /// <summary>
    /// Returns whether a purchase order can still be changed or canceled: until it is received or canceled.
    /// </summary>
    /// <param name="order">The purchase order.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool CanChange(PurchaseOrder order)
        => order is not null && order.Status is not (PurchaseOrderStatus.Received or PurchaseOrderStatus.Canceled);

    /// <summary>
    /// Makes a number staff can read out: the date and five characters made from the purchase order's place in a locked
    /// sequence, such as PO-261019-K3QZ7. Two purchase orders never get the same number.
    /// </summary>
    /// <param name="numberSequence">The locked number sequence.</param>
    /// <param name="now">The current UTC time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number.</returns>
    public static async Task<string> NewNumberAsync(INumberSequence numberSequence, DateTime now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numberSequence);

        var sequence = await numberSequence.NextAsync(NumberSeries, cancellationToken: cancellationToken);

        return $"PO-{now.ToString("yyMMdd", CultureInfo.InvariantCulture)}-{ReadableCodes.FromNumber(sequence, 5)}";
    }

    /// <summary>
    /// Applies a purchase order's details. The location changes only to a listed one, and only until some stock has
    /// arrived; the expected date is kept at noon UTC, so it reads the same in every time zone.
    /// </summary>
    /// <param name="order">The purchase order.</param>
    /// <param name="details">The details as entered.</param>
    /// <param name="locations">The listed stock locations.</param>
    /// <returns><see langword="false"/> when the supplier is missing, which is required.</returns>
    public static bool ApplyDetails(PurchaseOrder order, PurchaseOrderDetails details, IReadOnlyList<InventoryLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(details);

        order.Supplier = details.Supplier?.Trim();
        order.SupplierEmail = string.IsNullOrWhiteSpace(details.SupplierEmail) ? null : details.SupplierEmail.Trim();
        order.Note = string.IsNullOrWhiteSpace(details.Note) ? null : details.Note.Trim();
        order.Currency = string.IsNullOrWhiteSpace(details.Currency) ? order.Currency : details.Currency.Trim().ToUpperInvariant();

        // Noon UTC, so the date reads the same in every time zone.
        order.ExpectedUtc = details.ExpectedDate is { } expected ? DateTime.SpecifyKind(expected.Date.AddHours(12), DateTimeKind.Utc) : null;

        // Where the stock arrives can change until some of it has.
        if (order.Received == 0 && (locations ?? []).Any(location => location.LocationId == details.LocationId))
        {
            order.LocationId = details.LocationId;
        }

        return !string.IsNullOrWhiteSpace(details.Supplier);
    }

    /// <summary>
    /// Applies changes to a purchase order's lines and adds stock items to it. A draft's lines can be removed and their
    /// quantities changed; once ordered, only their costs. Stock items are added to a draft only, once each, with one
    /// unit, while it has fewer than <see cref="MaxLines"/> lines; an unknown or archived item is not added.
    /// </summary>
    /// <param name="order">The purchase order.</param>
    /// <param name="changes">The changes to its lines, by line id; lines not found are skipped.</param>
    /// <param name="addStockItemIds">The stock items to add.</param>
    /// <param name="items">The stock item store.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lines whose quantity or cost was refused, with why.</returns>
    public static async Task<IReadOnlyList<PurchaseOrderLineProblem>> ApplyLinesAsync(
        PurchaseOrder order,
        IEnumerable<PurchaseOrderLineChange> changes,
        IEnumerable<string> addStockItemIds,
        IStockItemStore items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(items);

        var problems = new List<PurchaseOrderLineProblem>();

        foreach (var change in changes ?? [])
        {
            if (order.Lines.FirstOrDefault(line => line.LineId == change?.LineId) is not { } line)
            {
                continue;
            }

            // A draft's lines can be removed and their quantities changed; once ordered, only the costs.
            if (order.IsDraft && change.Remove)
            {
                order.Lines.Remove(line);

                continue;
            }

            if (order.IsDraft)
            {
                if (change.Ordered is not (> 0 and <= MaxQuantity))
                {
                    problems.Add(new PurchaseOrderLineProblem(line, PurchaseOrderLineProblemKind.Quantity));
                }
                else
                {
                    line.Ordered = change.Ordered.Value;
                }
            }

            if (change.UnitCost is < 0)
            {
                problems.Add(new PurchaseOrderLineProblem(line, PurchaseOrderLineProblemKind.NegativeCost));
            }
            else
            {
                line.UnitCost = change.UnitCost ?? 0;
            }
        }

        if (!order.IsDraft)
        {
            return problems;
        }

        foreach (var stockItemId in (addStockItemIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (order.Lines.Count >= MaxLines)
            {
                break;
            }

            if (order.Lines.Any(line => line.StockItemId == stockItemId))
            {
                continue;
            }

            if (await items.FindByIdAsync(stockItemId, cancellationToken) is { Archived: false } item)
            {
                order.Lines.Add(NewLine(item, 1));
            }
        }

        return problems;
    }

    /// <summary>
    /// Lists the items kept at the location (that have had units there) at or below their low-stock threshold, each
    /// ordered up to twice its threshold. An item never stocked at a location isn't low there.
    /// </summary>
    /// <param name="items">The stock item store.</param>
    /// <param name="locationId">The location the stock arrives at.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The lines to start a purchase order with.</returns>
    public static async Task<List<PurchaseOrderLine>> LowStockLinesAsync(IStockItemStore items, string locationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var lines = new List<PurchaseOrderLine>();

        for (var skip = 0; skip < LowStockScanMaxItems && lines.Count < MaxLines; skip += LowStockScanPageSize)
        {
            var (_, page) = await items.PageAsync(new StockItemQuery(), skip, LowStockScanPageSize, cancellationToken);

            foreach (var item in page)
            {
                // An item counted before there were locations, with none left, is kept at the default location.
                var level = item.FindLevel(locationId) ??
                    (locationId == InventoryConstants.DefaultLocationId && item.Levels.Count == 0 ? new StockLocationLevel { LocationId = locationId } : null);

                if (level is null)
                {
                    continue;
                }

                var available = level.Available;

                if (available <= item.LowStockThreshold && lines.Count < MaxLines)
                {
                    lines.Add(NewLine(item, Math.Max(1, (item.LowStockThreshold * 2) - available)));
                }
            }

            if (page.Count < LowStockScanPageSize)
            {
                break;
            }
        }

        return lines;
    }

    private static PurchaseOrderLine NewLine(StockItem item, int quantity)
        => new()
        {
            LineId = UniqueId.GenerateId(),
            StockItemId = item.ItemId,
            Title = item.Title,
            Sku = item.Sku,
            Ordered = quantity,
        };
}

/// <summary>
/// A purchase order's details as entered.
/// </summary>
internal sealed class PurchaseOrderDetails
{
    public string Supplier { get; init; }

    public string SupplierEmail { get; init; }

    public string LocationId { get; init; }

    public string Currency { get; init; }

    public DateTime? ExpectedDate { get; init; }

    public string Note { get; init; }
}

/// <summary>
/// A change to one line of a purchase order: its quantity (a draft's), its cost, or its removal (from a draft).
/// </summary>
/// <param name="LineId">The line's id.</param>
/// <param name="Ordered">The units to order; required for a draft's line that stays.</param>
/// <param name="UnitCost">What one unit costs; zero when not given.</param>
/// <param name="Remove">Whether the line is removed from a draft.</param>
internal sealed record PurchaseOrderLineChange(string LineId, int? Ordered, decimal? UnitCost, bool Remove);

/// <summary>
/// A purchase order line whose change was refused.
/// </summary>
/// <param name="Line">The line.</param>
/// <param name="Kind">What was wrong.</param>
internal sealed record PurchaseOrderLineProblem(PurchaseOrderLine Line, PurchaseOrderLineProblemKind Kind);

/// <summary>
/// What was wrong with a purchase order line's change.
/// </summary>
internal enum PurchaseOrderLineProblemKind
{
    /// <summary>
    /// The quantity was missing or not between one and <see cref="PurchaseOrderRules.MaxQuantity"/>.
    /// </summary>
    Quantity,

    /// <summary>
    /// The cost was negative.
    /// </summary>
    NegativeCost,
}
