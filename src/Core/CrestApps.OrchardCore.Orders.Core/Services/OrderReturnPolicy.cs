using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// What a buyer can still send back: the goods of a paid order, within the return window, less what earlier returns
/// already cover.
/// </summary>
public static class OrderReturnPolicy
{
    /// <summary>
    /// Gets the units of each line, by line id, that can still be returned.
    /// </summary>
    public static IReadOnlyDictionary<string, int> GetReturnable(Order order, IEnumerable<OrderReturn> returns)
    {
        ArgumentNullException.ThrowIfNull(order);

        var taken = (returns ?? [])
            .Where(orderReturn => orderReturn.IsActive)
            .SelectMany(orderReturn => orderReturn.Lines)
            .GroupBy(line => line.LineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.Ordinal);

        // Downloads and services have nothing to send back.
        return order.Lines
            .Where(line => line.ProductType is ProductType.Good or ProductType.Undefined)
            .Select(line => (line.LineId, Left: line.Quantity - taken.GetValueOrDefault(line.LineId)))
            .Where(entry => entry.Left > 0)
            .ToDictionary(entry => entry.LineId, entry => entry.Left, StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns whether the order's buyer can still ask to return items: a paid order within the window.
    /// </summary>
    public static bool IsOpen(Order order, ReturnSettings settings, DateTime utcNow)
        => order is { Status: OrderStatus.Paid, PaidUtc: { } paid } &&
            paid.AddDays(Math.Max(0, settings?.WindowDays ?? 0)) >= utcNow;

    /// <summary>
    /// Gets what the returned units were paid, before tax and shipping: each line's price per unit.
    /// </summary>
    public static decimal GetItemsValue(OrderReturn orderReturn)
        => orderReturn.Lines.Sum(line => line.UnitPrice * line.Quantity);

    /// <summary>
    /// Gets what one unit of an order line was paid, add-ons and line discounts included.
    /// </summary>
    public static decimal UnitPriceOf(OrderLine line)
        => line.Quantity > 0 ? Math.Round(line.LineTotal / line.Quantity, 2, MidpointRounding.AwayFromZero) : line.UnitPrice;
}
