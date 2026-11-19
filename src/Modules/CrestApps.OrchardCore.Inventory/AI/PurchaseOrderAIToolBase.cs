using System.Globalization;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Inventory.AI;

/// <summary>
/// A purchase order AI tool. It finds a purchase order by id or number, describes it from an explicit list of fields,
/// and commits a change at once, as Commerce › Purchase orders does at the end of its request, so a change someone else
/// made to the same purchase order at the same moment is reported instead of answered as saved.
/// </summary>
internal abstract class PurchaseOrderAIToolBase : InventoryAIToolBase
{
    /// <summary>
    /// The statuses a purchase order list can show.
    /// </summary>
    protected static readonly IReadOnlyDictionary<string, PurchaseOrderStatus> Statuses = new Dictionary<string, PurchaseOrderStatus>(StringComparer.OrdinalIgnoreCase)
    {
        ["draft"] = PurchaseOrderStatus.Draft,
        ["ordered"] = PurchaseOrderStatus.Ordered,
        ["partiallyReceived"] = PurchaseOrderStatus.PartiallyReceived,
        ["received"] = PurchaseOrderStatus.Received,
        ["canceled"] = PurchaseOrderStatus.Canceled,
    };

    /// <summary>
    /// Finds a purchase order by its id or its number.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="purchaseOrderId">The purchase order's id, or its number, such as PO-261019-K3QZ7.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The purchase order, or the sentence that says it was not found.</returns>
    protected static async Task<(PurchaseOrder Order, string Error)> FindOrderAsync(IServiceProvider services, string purchaseOrderId, CancellationToken cancellationToken)
    {
        if (purchaseOrderId is null)
        {
            return (null, "Nothing was done: give the purchase order's id or number. Use listPurchaseOrders to find it.");
        }

        var store = services.GetRequiredService<IPurchaseOrderStore>();
        var order = await store.FindByIdAsync(purchaseOrderId, cancellationToken);

        if (order is null && purchaseOrderId.StartsWith("PO-", StringComparison.OrdinalIgnoreCase))
        {
            var (_, matches) = await store.PageAsync(new PurchaseOrderQuery { Search = purchaseOrderId, All = true }, 0, 10, cancellationToken);

            order = matches.FirstOrDefault(match => string.Equals(match.Number, purchaseOrderId, StringComparison.OrdinalIgnoreCase));
        }

        return order is null
            ? (null, $"Nothing was done: no purchase order has the id or number '{purchaseOrderId}'. Use listPurchaseOrders to find it.")
            : (order, null);
    }

    /// <summary>
    /// Saves a changed purchase order and commits it at once. The work of earlier tool calls of the chat message must
    /// have been written first (<see cref="InventoryAIToolBase.WriteEarlierWorkAsync"/>), so a refused commit discards
    /// only this change.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="order">The purchase order to save, or <see langword="null"/> when it is already saved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was saved, or the sentence that says why not.</returns>
    protected static async Task<string> CommitAsync(IServiceProvider services, PurchaseOrder order, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<YesSqlSession>();

        try
        {
            if (order is not null)
            {
                await services.GetRequiredService<IPurchaseOrderStore>().UpdateAsync(order, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();
            MarkSessionClosed(services);

            return "Nothing was saved: someone else changed this purchase order or its stock at the same moment. Read it again with getPurchaseOrder and retry in your next message.";
        }
    }

    /// <summary>
    /// Describes a purchase order for a list.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="order">The purchase order.</param>
    /// <param name="locations">The listed locations, to name the order's location.</param>
    /// <returns>The description.</returns>
    protected static async Task<object> SummarizeAsync(IServiceProvider services, PurchaseOrder order, IReadOnlyList<InventoryLocation> locations)
        => new
        {
            purchaseOrderId = order.ItemId,
            number = order.Number,
            supplier = order.Supplier,
            status = StatusName(order.Status),
            locationId = order.LocationId,
            location = LocationName(order.LocationId, locations),
            expectedDate = ExpectedDate(order),
            unitsOrdered = order.Ordered,
            unitsReceived = order.Received,
            unitsOutstanding = order.Lines.Sum(line => line.Outstanding),
            total = order.Total,
            currency = order.Currency,
            created = await LocalTimeAsync(services, order.CreatedUtc),
        };

    /// <summary>
    /// Describes a purchase order with its details and lines.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="order">The purchase order.</param>
    /// <param name="locations">The listed locations, to name the order's location.</param>
    /// <returns>The description.</returns>
    protected static async Task<object> DescribeAsync(IServiceProvider services, PurchaseOrder order, IReadOnlyList<InventoryLocation> locations)
        => new
        {
            purchaseOrderId = order.ItemId,
            number = order.Number,
            supplier = order.Supplier,
            supplierEmail = order.SupplierEmail,
            status = StatusName(order.Status),
            locationId = order.LocationId,
            location = LocationName(order.LocationId, locations),
            currency = order.Currency,
            expectedDate = ExpectedDate(order),
            note = order.Note,
            createdBy = order.Author,
            created = await LocalTimeAsync(services, order.CreatedUtc),
            orderedAt = await LocalTimeAsync(services, order.OrderedUtc),
            receivedAt = await LocalTimeAsync(services, order.ReceivedUtc),
            unitsOrdered = order.Ordered,
            unitsReceived = order.Received,
            unitsOutstanding = order.Lines.Sum(line => line.Outstanding),
            total = order.Total,
            canChange = PurchaseOrderRules.CanChange(order),
            canReceive = order.CanReceive,
            canDelete = order.IsDraft,
            lines = order.Lines.Select(line => new
            {
                lineId = line.LineId,
                stockItemId = line.StockItemId,
                sku = line.Sku,
                title = line.Title,
                ordered = line.Ordered,
                received = line.Received,
                outstanding = line.Outstanding,
                unitCost = line.UnitCost,
                lineTotal = line.UnitCost * line.Ordered,
            }).ToArray(),
        };

    /// <summary>
    /// Gets the name a result gives a status.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns>The name, such as <c>partiallyReceived</c>.</returns>
    protected static string StatusName(PurchaseOrderStatus status)
        => Statuses.FirstOrDefault(entry => entry.Value == status).Key ?? status.ToString();

    /// <summary>
    /// Reads a date argument written as yyyy-MM-dd.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="date">The date.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    protected static bool TryReadDate(string text, out DateTime date)
        => DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static string LocationName(string locationId, IReadOnlyList<InventoryLocation> locations)
        => locations?.FirstOrDefault(location => location.LocationId == locationId)?.Name ?? locationId;

    // The expected date is a pure date kept at noon UTC, so it is the same day in every time zone.
    private static string ExpectedDate(PurchaseOrder order)
        => order.ExpectedUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
