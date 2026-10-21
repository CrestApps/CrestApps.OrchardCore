using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Transfer;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Orders as a spreadsheet: a row per line, with the order's totals on its first line only, so adding up a column
/// counts each order once.
/// </summary>
public static class OrderCsv
{
    public static readonly string[] Columns =
    [
        "Order number", "Placed", "Status", "Customer", "Email", "Item", "SKU", "Quantity", "Unit price", "Line total",
        "Subtotal", "Discounts", "Shipping", "Tax", "Total", "Refunded", "Currency",
    ];

    /// <summary>
    /// Writes the orders.
    /// </summary>
    /// <param name="orders">The orders.</param>
    /// <param name="toLocal">Turns a stored UTC time into the site's time.</param>
    /// <param name="owedOrderIds">The orders still owed, such as ones paid by bank transfer before the money arrives,
    /// whose status reads "Payment due".</param>
    public static string Write(IEnumerable<Order> orders, Func<DateTime, DateTime> toLocal, ISet<string> owedOrderIds = null)
    {
        ArgumentNullException.ThrowIfNull(toLocal);

        var csv = new StringBuilder();

        AppendRow(csv, Columns);

        foreach (var order in orders ?? [])
        {
            var placed = toLocal(order.CreatedUtc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var lines = order.Lines is { Count: > 0 } ? order.Lines : [null];
            var first = true;

            foreach (var line in lines)
            {
                AppendRow(csv,
                [
                    order.OrderNumber,
                    placed,
                    owedOrderIds?.Contains(order.ItemId) == true ? "Payment due" : order.Status.ToString(),
                    order.CustomerName,
                    order.CustomerEmail,
                    line is null ? null : OrderLineText.Describe(line),
                    line?.Sku,
                    line?.Quantity.ToString(CultureInfo.InvariantCulture),
                    Money(line?.UnitPrice),
                    Money(line?.LineTotal),
                    first ? Money(order.Subtotal) : null,
                    first ? Money(order.Discounts?.Sum(discount => discount.Amount)) : null,
                    first ? Money(order.Shipping?.Amount) : null,
                    first ? Money(order.TaxAmount) : null,
                    first ? Money(order.Total) : null,
                    first ? Money(order.RefundedAmount) : null,
                    order.Currency,
                ]);

                first = false;
            }
        }

        return csv.ToString();
    }

    private static string Money(decimal? amount)
        => amount?.ToString("0.00", CultureInfo.InvariantCulture);

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
        => CsvTable.AppendRow(csv, values);
}
