using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Reports;

/// <summary>
/// The arithmetic behind the order reports, kept apart from reading orders so it can be checked on its own. Every
/// figure is per currency: amounts in two currencies are never added.
/// </summary>
public static class OrderReportAggregator
{
    /// <summary>
    /// Sums the orders per currency, the currency with the most orders first.
    /// </summary>
    public static IReadOnlyList<OrderSalesTotals> Summarize(IEnumerable<Order> orders)
        => [.. orders
            .GroupBy(order => order.Currency ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => Sum(group.Key, group))
            .OrderByDescending(totals => totals.Orders)
            .ThenBy(totals => totals.Currency, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Sums one currency's orders by the day, or the month, they were paid on in <paramref name="timeZone"/>.
    /// </summary>
    public static IReadOnlyList<OrderSalesBucket> Bucket(IEnumerable<Order> orders, TimeZoneInfo timeZone, bool byMonth)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        return [.. orders
            .Where(order => order.PaidUtc.HasValue)
            .GroupBy(order =>
            {
                var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(order.PaidUtc.Value, DateTimeKind.Utc), timeZone));

                return byMonth ? new DateOnly(local.Year, local.Month, 1) : local;
            })
            .OrderBy(group => group.Key)
            .Select(group => new OrderSalesBucket(group.Key, Sum(group.First().Currency, group)))];
    }

    /// <summary>
    /// Sums what each product, and each of its variants, sold: units and line totals before discounts.
    /// </summary>
    public static IReadOnlyList<ProductSales> ByProduct(IEnumerable<Order> orders)
        => [.. orders
            .SelectMany(order => (order.Lines ?? []).Select(line => (order.Currency, Line: line)))
            .GroupBy(entry => (entry.Line.ContentItemId, entry.Line.VariantId, Currency: entry.Currency ?? string.Empty))
            .Select(group =>
            {
                var first = group.First().Line;
                var title = string.IsNullOrEmpty(first.VariantTitle) ? first.Title : $"{first.Title} ({first.VariantTitle})";

                return new ProductSales(title, first.Sku, group.Key.Currency, group.Sum(entry => entry.Line.Quantity), group.Sum(entry => entry.Line.LineTotal), group.Count());
            })
            .OrderByDescending(product => product.Sales)
            .ThenBy(product => product.Title, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Sums the orders by the country they ship to, or are billed to when nothing ships.
    /// </summary>
    public static IReadOnlyList<CountrySales> ByCountry(IEnumerable<Order> orders)
        => [.. orders
            .GroupBy(order => (Country: order.ShippingAddress?.Country ?? order.BillingAddress?.Country ?? string.Empty, Currency: order.Currency ?? string.Empty))
            .Select(group => new CountrySales(group.Key.Country, group.Key.Currency, group.Count(), group.Sum(order => order.Total ?? order.Subtotal), group.Sum(order => order.TaxAmount ?? 0m)))
            .OrderByDescending(country => country.Total)];

    /// <summary>
    /// Sums the discounts by code: how many orders used each and what it took off.
    /// </summary>
    public static IReadOnlyList<DiscountUse> ByDiscount(IEnumerable<Order> orders)
        => [.. orders
            .SelectMany(order => (order.Discounts ?? []).Select(discount => (order.Currency, Discount: discount)))
            .GroupBy(entry => (Code: entry.Discount.Code ?? entry.Discount.Description ?? string.Empty, Currency: entry.Currency ?? string.Empty), new CodeComparer())
            .Select(group => new DiscountUse(group.Key.Code, group.Key.Currency, group.Count(), group.Sum(entry => entry.Discount.Amount)))
            .OrderByDescending(use => use.Amount)];

    /// <summary>
    /// Lists the refunds given from <paramref name="fromUtc"/> (inclusive) to <paramref name="toUtc"/> (exclusive), newest
    /// first, with their orders.
    /// </summary>
    public static IReadOnlyList<(Order Order, OrderRefund Refund)> Refunds(IEnumerable<Order> orders, DateTime fromUtc, DateTime toUtc)
        => [.. orders
            .SelectMany(order => (order.Refunds ?? []).Select(refund => (order, refund)))
            .Where(entry => entry.refund.CreatedUtc >= fromUtc && entry.refund.CreatedUtc < toUtc)
            .OrderByDescending(entry => entry.refund.CreatedUtc)];

    private static OrderSalesTotals Sum(string currency, IEnumerable<Order> orders)
    {
        var totals = new OrderSalesTotals { Currency = currency ?? string.Empty };

        foreach (var order in orders)
        {
            totals.Orders++;
            totals.Units += (order.Lines ?? []).Sum(line => line.Quantity);
            totals.Gross += order.Subtotal;
            totals.Discounts += order.DiscountTotal;
            totals.Shipping += order.Shipping?.Amount ?? 0m;
            totals.Tax += order.TaxAmount ?? 0m;
            totals.Total += order.Total ?? order.Subtotal;
            totals.Refunded += order.RefundedAmount;
        }

        return totals;
    }

    private sealed class CodeComparer : IEqualityComparer<(string Code, string Currency)>
    {
        public bool Equals((string Code, string Currency) x, (string Code, string Currency) y)
            => string.Equals(x.Code, y.Code, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Currency, y.Currency, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Code, string Currency) obj)
            => HashCode.Combine(obj.Code?.ToUpperInvariant(), obj.Currency?.ToUpperInvariant());
    }
}

/// <summary>
/// What a set of orders in one currency came to.
/// </summary>
public sealed class OrderSalesTotals
{
    public string Currency { get; set; }

    public int Orders { get; set; }

    public int Units { get; set; }

    /// <summary>
    /// Gets or sets the line totals, before discounts, shipping and tax.
    /// </summary>
    public decimal Gross { get; set; }

    public decimal Discounts { get; set; }

    public decimal Shipping { get; set; }

    public decimal Tax { get; set; }

    /// <summary>
    /// Gets or sets what was charged: the lines less discounts, plus shipping and tax.
    /// </summary>
    public decimal Total { get; set; }

    public decimal Refunded { get; set; }

    /// <summary>
    /// Gets what was kept: what was charged less what was given back.
    /// </summary>
    public decimal Net
        => Total - Refunded;
}

public sealed record OrderSalesBucket(DateOnly Start, OrderSalesTotals Totals);

public sealed record ProductSales(string Title, string Sku, string Currency, int Units, decimal Sales, int Orders);

public sealed record CountrySales(string Country, string Currency, int Orders, decimal Total, decimal Tax);

public sealed record DiscountUse(string Code, string Currency, int Orders, decimal Amount);
