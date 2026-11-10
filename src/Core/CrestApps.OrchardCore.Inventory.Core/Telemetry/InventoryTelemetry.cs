using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Inventory.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Telemetry;

/// <summary>
/// The OpenTelemetry instruments and spans of inventory, under the meter and activity source named
/// <c>CrestApps.OrchardCore.Inventory</c>: stock count imports, stock movements and the holds orders put on stock.
/// </summary>
/// <remarks>
/// Stock changes are made in the caller's unit of work, so the movements and holds are counted when the change is made;
/// a unit of work that is rolled back afterwards does not take them back. No instrument carries an item, order, location
/// or user id.
/// </remarks>
public static class InventoryTelemetry
{
    /// <summary>
    /// The area of the inventory instruments, which names their meter and activity source.
    /// </summary>
    public const string Area = "Inventory";

    /// <summary>
    /// The name of the span of one stock count import.
    /// </summary>
    public const string StockCountSpanName = "crestapps.inventory.stock_count.import";

    /// <summary>
    /// The metric tag that records the kind of a stock movement.
    /// </summary>
    public const string TypeTag = "type";

    /// <summary>
    /// The stock count import stopped at the row limit, so the rows after it were not read.
    /// </summary>
    public const string OutcomeTruncated = "truncated";

    /// <summary>
    /// The row gave the item a new count.
    /// </summary>
    public const string RowCounted = "counted";

    /// <summary>
    /// The row gave the count the item already had.
    /// </summary>
    public const string RowUnchanged = "unchanged";

    /// <summary>
    /// No stock item matched the row.
    /// </summary>
    public const string RowNotFound = "not_found";

    /// <summary>
    /// The row did not give a whole number of zero or more.
    /// </summary>
    public const string RowInvalidCount = "invalid_count";

    /// <summary>
    /// An earlier row counted the same item.
    /// </summary>
    public const string RowDuplicate = "duplicate";

    /// <summary>
    /// The inventory refused the new count.
    /// </summary>
    public const string RowRejected = "rejected";

    /// <summary>
    /// Units were held for an order.
    /// </summary>
    public const string HoldPlaced = "placed";

    /// <summary>
    /// A hold became a sale when the order was paid.
    /// </summary>
    public const string HoldConverted = "converted";

    /// <summary>
    /// A paid order was sold units that were no longer held for it, such as after its hold expired.
    /// </summary>
    public const string HoldCommittedWithoutHold = "committed_without_hold";

    /// <summary>
    /// A hold was let go, such as when the order was cancelled.
    /// </summary>
    public const string HoldReleased = "released";

    /// <summary>
    /// A hold was let go because the order was not paid before the hold expired.
    /// </summary>
    public const string HoldExpired = "expired";

    /// <summary>
    /// Gets the meter of the inventory instruments.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    /// <summary>
    /// Gets the activity source of the inventory spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// Gets the duration of stock count imports, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>:
    /// <c>ok</c>, <see cref="OutcomeTruncated"/>, <c>error</c> or <c>cancelled</c>.
    /// </summary>
    public static Histogram<double> StockCountDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.inventory.stock_count.duration",
        "The duration of stock count imports, from a file or a recipe.");

    /// <summary>
    /// Gets the number of rows stock count imports read, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>.
    /// </summary>
    public static Counter<long> StockCountRows { get; } = Meter.CreateCounter<long>(
        "crestapps.inventory.stock_count.rows",
        "{row}",
        "The number of rows stock count imports read, by what became of them.");

    /// <summary>
    /// Gets the number of stock movements, tagged with the <see cref="TypeTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.DirectionTag"/>: <c>in</c>, <c>out</c> or <c>none</c>.
    /// </summary>
    public static Counter<long> Movements { get; } = Meter.CreateCounter<long>(
        "crestapps.inventory.movements",
        "{movement}",
        "The number of stock movements, by kind and direction.");

    /// <summary>
    /// Gets the number of units stock movements moved, tagged as <see cref="Movements"/> is.
    /// </summary>
    public static Counter<long> MovementUnits { get; } = Meter.CreateCounter<long>(
        "crestapps.inventory.movement.units",
        "{unit}",
        "The number of units stock movements moved, by kind and direction.");

    /// <summary>
    /// Gets the number of holds orders put on stock that changed state, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>: <see cref="HoldPlaced"/>, <see cref="HoldConverted"/>,
    /// <see cref="HoldCommittedWithoutHold"/>, <see cref="HoldReleased"/> or <see cref="HoldExpired"/>.
    /// </summary>
    public static Counter<long> Holds { get; } = Meter.CreateCounter<long>(
        "crestapps.inventory.holds",
        "{hold}",
        "The number of stock holds placed, converted to sales, released or expired.");

    /// <summary>
    /// Gets the number of orders whose stock could not be held because a line was short.
    /// </summary>
    public static Counter<long> HoldRefusals { get; } = Meter.CreateCounter<long>(
        "crestapps.inventory.hold.refusals",
        "{order}",
        "The number of orders whose stock could not be held because a line was short.");

    /// <summary>
    /// Records one stock movement.
    /// </summary>
    /// <param name="kind">The kind of the movement.</param>
    /// <param name="quantity">The units the movement added, or removed when negative.</param>
    public static void RecordMovement(StockMovementKind kind, int quantity)
    {
        if (!Movements.Enabled && !MovementUnits.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { TypeTag, GetMovementType(kind) },
            { CrestAppsOrchardCoreTelemetry.DirectionTag, quantity > 0 ? "in" : quantity < 0 ? "out" : "none" },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Movements.Add(1, tags);

        if (quantity != 0)
        {
            MovementUnits.Add(Math.Abs((long)quantity), tags);
        }
    }

    /// <summary>
    /// Records holds that changed state the same way.
    /// </summary>
    /// <param name="outcome">What became of the holds, such as <see cref="HoldPlaced"/>.</param>
    /// <param name="count">The number of holds. Zero and negative counts are ignored.</param>
    public static void RecordHolds(string outcome, long count)
    {
        if (count <= 0 || !Holds.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Holds.Add(count, tags);
    }

    /// <summary>
    /// Records an order whose stock could not be held because a line was short.
    /// </summary>
    public static void RecordHoldRefusal()
    {
        if (!HoldRefusals.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, "shortage" },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        HoldRefusals.Add(1, tags);
    }

    /// <summary>
    /// Records rows of a stock count import that ended the same way.
    /// </summary>
    /// <param name="result">What became of the rows, such as <see cref="RowCounted"/>.</param>
    /// <param name="count">The number of rows. Zero and negative counts are ignored.</param>
    public static void RecordStockCountRows(string result, long count)
    {
        if (count <= 0 || !StockCountRows.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        StockCountRows.Add(count, tags);
    }

    /// <summary>
    /// Gets the bounded <see cref="TypeTag"/> value of a stock movement kind.
    /// </summary>
    /// <param name="kind">The kind of the movement.</param>
    /// <returns>The kind in snake case, or <see cref="CrestAppsOrchardCoreTelemetry.Other"/> for a kind this version does not know.</returns>
    public static string GetMovementType(StockMovementKind kind)
    {
        return kind switch
        {
            StockMovementKind.Received => "received",
            StockMovementKind.Adjusted => "adjusted",
            StockMovementKind.Counted => "counted",
            StockMovementKind.Sold => "sold",
            StockMovementKind.Returned => "returned",
            StockMovementKind.Transferred => "transferred",
            StockMovementKind.Consumed => "consumed",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }
}
