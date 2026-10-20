using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

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

    /// <summary>
    /// Sums what each kind of product (physical, digital, service, subscription) sold: units and line totals before
    /// discounts.
    /// </summary>
    public static IReadOnlyList<ProductTypeSales> ByProductType(IEnumerable<Order> orders)
        => [.. orders
            .SelectMany(order => (order.Lines ?? []).Select(line => (order.ItemId, order.Currency, Line: line)))
            .GroupBy(entry => (entry.Line.ProductType, Currency: entry.Currency ?? string.Empty))
            .Select(group => new ProductTypeSales(group.Key.ProductType, group.Key.Currency, group.Select(entry => entry.ItemId).Distinct(StringComparer.Ordinal).Count(), group.Sum(entry => entry.Line.Quantity), group.Sum(entry => entry.Line.LineTotal)))
            .OrderByDescending(type => type.Sales)];

    /// <summary>
    /// Sums what each term of each taxonomy sold, such as each category and each brand. <paramref name="taxonomyOf"/>
    /// names the taxonomy a term belongs to, or returns <see langword="null"/> for a term no taxonomy has any more. A line
    /// with two terms of one taxonomy counts in both; a line with none of a taxonomy's terms counts under that taxonomy
    /// with an empty term id, so each taxonomy's rows add up to every line.
    /// </summary>
    public static IReadOnlyList<CategorySales> ByCategory(IEnumerable<Order> orders, Func<string, string> taxonomyOf)
    {
        ArgumentNullException.ThrowIfNull(taxonomyOf);

        var lines = orders
            .SelectMany(order => (order.Lines ?? []).Select(line => (order.ItemId, Currency: order.Currency ?? string.Empty, Line: line, Terms: (line.CategoryIds ?? []).Distinct(StringComparer.Ordinal).Select(id => (Id: id, Taxonomy: taxonomyOf(id) ?? string.Empty)).ToList())))
            .ToList();

        var taxonomies = lines.SelectMany(line => line.Terms.Select(term => term.Taxonomy)).Distinct(StringComparer.Ordinal).ToList();

        return [.. lines
            .SelectMany(line => taxonomies.SelectMany(taxonomy =>
            {
                var terms = line.Terms.Where(term => term.Taxonomy == taxonomy).Select(term => term.Id).ToList();

                return (terms.Count == 0 ? [string.Empty] : terms).Select(termId => (line.ItemId, line.Currency, line.Line, Taxonomy: taxonomy, TermId: termId));
            }))
            .GroupBy(entry => (entry.Taxonomy, entry.TermId, entry.Currency))
            .Select(group => new CategorySales(group.Key.Taxonomy, group.Key.TermId, group.Key.Currency, group.Select(entry => entry.ItemId).Distinct(StringComparer.Ordinal).Count(), group.Sum(entry => entry.Line.Quantity), group.Sum(entry => entry.Line.LineTotal)))
            .OrderBy(category => category.Taxonomy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => string.IsNullOrEmpty(category.CategoryId) ? 1 : 0)
            .ThenByDescending(category => category.Sales)];
    }

    /// <summary>
    /// Who bought in the period, per currency: how many were buying for the first time and how many came back, and what
    /// each has spent in all, from <paramref name="history"/> (every paid order up to the period's end, the period's own
    /// included). A buyer is known by their customer record, else their account, else their email.
    /// </summary>
    public static IReadOnlyList<CustomerPurchases> Customers(IEnumerable<Order> periodOrders, IEnumerable<Order> history, DateTime fromUtc)
    {
        var byBuyer = history
            .Select(order => (Key: BuyerKey(order), Order: order))
            .Where(entry => entry.Key is not null)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Order).ToList(), StringComparer.Ordinal);

        var result = new List<CustomerPurchases>();

        foreach (var currency in periodOrders.GroupBy(order => order.Currency ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var buyers = new List<BuyerPurchases>();

            foreach (var group in currency.Select(order => (Key: BuyerKey(order), Order: order)).Where(entry => entry.Key is not null).GroupBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var all = byBuyer.TryGetValue(group.Key, out var known) ? known : [.. group.Select(entry => entry.Order)];
                var inCurrency = all.Where(order => string.Equals(order.Currency ?? string.Empty, currency.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                var latest = group.Select(entry => entry.Order).OrderByDescending(order => order.PaidUtc).First();

                buyers.Add(new BuyerPurchases(
                    latest.CustomerName,
                    latest.CustomerEmail,
                    OrdersInPeriod: group.Count(),
                    SpentInPeriod: group.Sum(entry => entry.Order.Total ?? entry.Order.Subtotal),
                    LifetimeOrders: inCurrency.Count,
                    LifetimeSpent: inCurrency.Sum(order => (order.Total ?? order.Subtotal) - order.RefundedAmount),
                    FirstPaidUtc: all.Min(order => order.PaidUtc ?? order.CreatedUtc),
                    IsNew: all.All(order => (order.PaidUtc ?? order.CreatedUtc) >= fromUtc)));
            }

            result.Add(new CustomerPurchases(currency.Key, [.. buyers.OrderByDescending(buyer => buyer.LifetimeSpent).ThenBy(buyer => buyer.Email, StringComparer.OrdinalIgnoreCase)]));
        }

        return [.. result.OrderByDescending(entry => entry.Buyers.Count)];
    }

    /// <summary>
    /// Counts the order checkouts started in a period by how they ended.
    /// </summary>
    public static CheckoutFunnel Funnel(IEnumerable<CheckoutSessionStatus> checkouts)
    {
        var funnel = new CheckoutFunnel();

        foreach (var status in checkouts)
        {
            funnel.Started++;

            switch (status)
            {
                case CheckoutSessionStatus.Completed:
                    funnel.Paid++;
                    break;
                case CheckoutSessionStatus.Failed:
                    funnel.Failed++;
                    break;
                case CheckoutSessionStatus.Canceled:
                case CheckoutSessionStatus.Expired:
                    funnel.Abandoned++;
                    break;
                default:
                    funnel.Open++;
                    break;
            }
        }

        return funnel;
    }

    internal static string BuyerKey(Order order)
    {
        if (!string.IsNullOrEmpty(order.CustomerId))
        {
            return "c:" + order.CustomerId;
        }

        if (!string.IsNullOrEmpty(order.UserId))
        {
            return "u:" + order.UserId;
        }

        return string.IsNullOrWhiteSpace(order.CustomerEmail) ? null : "e:" + order.CustomerEmail.Trim().ToUpperInvariant();
    }

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

    /// <summary>
    /// Gets what an order came to on average: what was charged, over the orders.
    /// </summary>
    public decimal AverageOrderValue
        => Orders == 0 ? 0m : Math.Round(Total / Orders, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Gets how many items an order held on average.
    /// </summary>
    public decimal ItemsPerOrder
        => Orders == 0 ? 0m : Math.Round((decimal)Units / Orders, 1, MidpointRounding.AwayFromZero);
}

/// <summary>
/// How the order checkouts started in a period ended.
/// </summary>
public sealed class CheckoutFunnel
{
    public int Started { get; set; }

    public int Paid { get; set; }

    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the checkouts the buyer left, or that expired before they paid.
    /// </summary>
    public int Abandoned { get; set; }

    /// <summary>
    /// Gets or sets the checkouts still going, or waiting for the payment provider.
    /// </summary>
    public int Open { get; set; }

    /// <summary>
    /// Gets the share of checkouts that were paid, from 0 to 1.
    /// </summary>
    public double PaidRate
        => Started == 0 ? 0d : (double)Paid / Started;
}

public sealed record OrderSalesBucket(DateOnly Start, OrderSalesTotals Totals);

public sealed record ProductSales(string Title, string Sku, string Currency, int Units, decimal Sales, int Orders);

public sealed record CountrySales(string Country, string Currency, int Orders, decimal Total, decimal Tax);

public sealed record DiscountUse(string Code, string Currency, int Orders, decimal Amount);

public sealed record ProductTypeSales(ProductType ProductType, string Currency, int Orders, int Units, decimal Sales);

public sealed record CategorySales(string Taxonomy, string CategoryId, string Currency, int Orders, int Units, decimal Sales);

/// <summary>
/// The buyers of a period in one currency.
/// </summary>
public sealed record CustomerPurchases(string Currency, IReadOnlyList<BuyerPurchases> Buyers)
{
    public int NewBuyers
        => Buyers.Count(buyer => buyer.IsNew);

    public int ReturningBuyers
        => Buyers.Count - NewBuyers;

    /// <summary>
    /// Gets the buyers who have paid for more than one order, ever.
    /// </summary>
    public int RepeatBuyers
        => Buyers.Count(buyer => buyer.LifetimeOrders > 1);

    public decimal AverageLifetimeSpent
        => Buyers.Count == 0 ? 0m : Math.Round(Buyers.Sum(buyer => buyer.LifetimeSpent) / Buyers.Count, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// One buyer of the period: what they bought in it, and what they have bought in all. Lifetime spend is net of refunds.
/// </summary>
public sealed record BuyerPurchases(string Name, string Email, int OrdersInPeriod, decimal SpentInPeriod, int LifetimeOrders, decimal LifetimeSpent, DateTime FirstPaidUtc, bool IsNew);
