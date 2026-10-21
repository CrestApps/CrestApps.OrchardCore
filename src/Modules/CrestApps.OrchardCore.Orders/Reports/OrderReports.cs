using System.Globalization;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Reports;

/// <summary>
/// What the order reports share: the orders paid in the chosen period, the site's time zone for days, and money in each
/// order's own currency.
/// </summary>
public abstract class OrderReportBase : IReportRunner
{
    // Without a start, a report reads from a date older than any order rather than from the beginning of time.
    private static readonly DateTime _earliest = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly OrderReportReader _reader;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    protected OrderReportBase(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer stringLocalizer)
    {
        _reader = reader;
        _siteService = siteService;
        _clock = clock;
        S = stringLocalizer;
    }

    public abstract Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);

    protected (DateTime FromUtc, DateTime ToUtc) GetPeriod(ReportContext context)
    {
        var range = context.Filter.GetDateRange();

        return (range.FromUtc ?? _earliest, range.ToUtc ?? _clock.UtcNow);
    }

    protected async Task<List<Order>> ReadPaidAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var orders = new List<Order>();

        await foreach (var order in _reader.ReadPaidAsync(fromUtc, toUtc, cancellationToken))
        {
            orders.Add(order);
        }

        return orders;
    }

    protected async Task<List<Order>> ReadUnfulfilledAsync(CancellationToken cancellationToken)
    {
        var orders = new List<Order>();

        await foreach (var order in _reader.ReadUnfulfilledAsync(cancellationToken))
        {
            orders.Add(order);
        }

        return orders;
    }

    protected async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrEmpty(site?.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var timeZone))
        {
            return timeZone;
        }

        return TimeZoneInfo.Utc;
    }

    protected DateTime UtcNow
        => _clock.UtcNow;

    protected static string Money(decimal amount, string currency)
        => ProductPriceFormatter.Format(amount, currency);

    protected static string Day(DateTime utc, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected ReportSection NoOrders()
        => ReportSection.ForMetrics(S["Summary"].Value, [new ReportMetric(S["Orders"].Value, "0", S["No orders were paid in this period."].Value)]);
}

/// <summary>
/// Orders paid in the period: what they came to, discounts, shipping, tax and refunds, by day or month.
/// </summary>
public sealed class OrderSalesReport : OrderReportBase, IReport
{
    public OrderSalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderSalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-sales";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderSalesReport>("Sales");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderSalesReport>("What the orders paid in the period came to, with discounts, shipping, tax and refunds, day by day.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var timeZone = await GetTimeZoneAsync();
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        // A long period reads better by month than by hundreds of days.
        var byMonth = (to - from).TotalDays > 62;

        foreach (var totals in OrderReportAggregator.Summarize(orders))
        {
            var currency = totals.Currency;

            document.Add(ReportSection.ForMetrics(S["Summary ({0})", currency].Value,
            [
                new ReportMetric(S["Orders"].Value, ReportFormat.Number(totals.Orders)),
                new ReportMetric(S["Items sold"].Value, ReportFormat.Number(totals.Units)),
                new ReportMetric(S["Average order value"].Value, Money(totals.AverageOrderValue, currency), S["What was charged, over the orders."].Value),
                new ReportMetric(S["Items per order"].Value, totals.ItemsPerOrder.ToString("0.0", CultureInfo.CurrentCulture)),
                new ReportMetric(S["Gross sales"].Value, Money(totals.Gross, currency), S["The lines, before discounts, shipping and tax."].Value),
                new ReportMetric(S["Discounts"].Value, Money(totals.Discounts, currency)),
                new ReportMetric(S["Shipping"].Value, Money(totals.Shipping, currency)),
                new ReportMetric(S["Tax"].Value, Money(totals.Tax, currency)),
                new ReportMetric(S["Total charged"].Value, Money(totals.Total, currency)),
                new ReportMetric(S["Refunded"].Value, Money(totals.Refunded, currency), S["Given back for these orders, whenever it was."].Value),
                new ReportMetric(S["Net"].Value, Money(totals.Net, currency)),
            ]));

            var buckets = OrderReportAggregator.Bucket(orders.Where(order => string.Equals(order.Currency ?? string.Empty, currency, StringComparison.OrdinalIgnoreCase)), timeZone, byMonth);
            var format = byMonth ? "yyyy-MM" : "yyyy-MM-dd";

            document.Add(ReportSection.ForChart(byMonth ? S["Sales by month ({0})", currency].Value : S["Sales by day ({0})", currency].Value, new ReportChart
            {
                Type = ReportChartType.Bar,
                Labels = [.. buckets.Select(bucket => bucket.Start.ToString(format, CultureInfo.InvariantCulture))],
                Datasets =
                [
                    new ReportChartDataset(S["Total charged"].Value, buckets.Select(bucket => (double)bucket.Totals.Total)),
                    new ReportChartDataset(S["Net"].Value, buckets.Select(bucket => (double)bucket.Totals.Net)),
                ],
                ShowLegend = true,
            }, 12));

            var rows = buckets
                .Select(bucket => Row(bucket.Start.ToString(format, CultureInfo.InvariantCulture), bucket.Totals, emphasize: false))
                .ToList();

            rows.Add(Row(S["Total"].Value, totals, emphasize: true));

            document.Add(ReportSection.ForTable(byMonth ? S["By month ({0})", currency].Value : S["By day ({0})", currency].Value,
            [
                new ReportColumn(byMonth ? S["Month"].Value : S["Day"].Value),
                new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Gross sales"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Discounts"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Shipping"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Total charged"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average order value"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Refunded"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Net"].Value, ReportColumnAlign.End),
            ], rows));
        }

        return document;
    }

    private static ReportRow Row(string label, OrderSalesTotals totals, bool emphasize)
        => new(
        [
            label,
            ReportFormat.Number(totals.Orders),
            Money(totals.Gross, totals.Currency),
            Money(totals.Discounts, totals.Currency),
            Money(totals.Shipping, totals.Currency),
            Money(totals.Tax, totals.Currency),
            Money(totals.Total, totals.Currency),
            Money(totals.AverageOrderValue, totals.Currency),
            Money(totals.Refunded, totals.Currency),
            Money(totals.Net, totals.Currency),
        ], emphasize);
}

/// <summary>
/// What each product sold in the period.
/// </summary>
public sealed class OrderProductSalesReport : OrderReportBase, IReport
{
    public OrderProductSalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderProductSalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-product-sales";

    private string KindName(ProductType productType)
        => productType switch
        {
            ProductType.Good => S["Goods"].Value,
            ProductType.Digital => S["Digital"].Value,
            ProductType.Service => S["Services"].Value,
            _ => S["Other"].Value,
        };

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderProductSalesReport>("Sales by product");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderProductSalesReport>("The units each product and variant sold in the period, and what they sold for before discounts.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        var products = OrderReportAggregator.ByProduct(orders);

        var byUnits = products.OrderByDescending(product => product.Units).Take(10).ToArray();
        var most = Math.Max(1, byUnits.Length == 0 ? 1 : byUnits[0].Units);

        document.Add(ReportSection.ForBars(S["Top products by units"].Value, [.. byUnits.Select(product => new ReportBar(product.Title, ReportFormat.Number(product.Units), (double)product.Units / most))]));

        var rows = products.Select(product => new ReportRow(
        [
            product.Title,
            product.Sku ?? string.Empty,
            ReportFormat.Number(product.Orders),
            ReportFormat.Number(product.Units),
            Money(product.Sales, product.Currency),
        ])).ToList();

        document.Add(ReportSection.ForTable(S["Products"].Value,
        [
            new ReportColumn(S["Product"].Value),
            new ReportColumn(S["SKU"].Value),
            new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Units"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Sales"].Value, ReportColumnAlign.End),
        ], rows));

        document.Add(ReportSection.ForTable(S["By kind of product"].Value,
        [
            new ReportColumn(S["Kind"].Value),
            new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Units"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Sales"].Value, ReportColumnAlign.End),
        ], [.. OrderReportAggregator.ByProductType(orders).Select(type => new ReportRow(
        [
            KindName(type.ProductType),
            ReportFormat.Number(type.Orders),
            ReportFormat.Number(type.Units),
            Money(type.Sales, type.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// What the orders paid in the period came to in each country they ship to.
/// </summary>
public sealed class OrderCountrySalesReport : OrderReportBase, IReport
{
    public OrderCountrySalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderCountrySalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-country-sales";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderCountrySalesReport>("Sales by country");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderCountrySalesReport>("Orders paid in the period and what they came to, by the country they ship to, or are billed to when nothing ships.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        var rows = OrderReportAggregator.ByCountry(orders).Select(country => new ReportRow(
        [
            string.IsNullOrEmpty(country.Country) ? S["No address"].Value : country.Country,
            ReportFormat.Number(country.Orders),
            Money(country.Tax, country.Currency),
            Money(country.Total, country.Currency),
        ])).ToList();

        document.Add(ReportSection.ForTable(S["Countries"].Value,
        [
            new ReportColumn(S["Country"].Value),
            new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total charged"].Value, ReportColumnAlign.End),
        ], rows));

        return document;
    }
}

/// <summary>
/// The tax charged on the orders paid in the period, by day or month.
/// </summary>
public sealed class OrderTaxReport : OrderReportBase, IReport
{
    public OrderTaxReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderTaxReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-tax";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderTaxReport>("Tax on orders");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderTaxReport>("The tax charged on the orders paid in the period, and what it was charged on, day by day.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var timeZone = await GetTimeZoneAsync();
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        var byMonth = (to - from).TotalDays > 62;
        var format = byMonth ? "yyyy-MM" : "yyyy-MM-dd";

        foreach (var totals in OrderReportAggregator.Summarize(orders))
        {
            var currency = totals.Currency;
            var taxed = totals.Total - totals.Tax;

            document.Add(ReportSection.ForMetrics(S["Summary ({0})", currency].Value,
            [
                new ReportMetric(S["Tax charged"].Value, Money(totals.Tax, currency)),
                new ReportMetric(S["Charged before tax"].Value, Money(taxed, currency), S["The lines less discounts, plus shipping."].Value),
                new ReportMetric(S["Orders"].Value, ReportFormat.Number(totals.Orders)),
            ]));

            var buckets = OrderReportAggregator.Bucket(orders.Where(order => string.Equals(order.Currency ?? string.Empty, currency, StringComparison.OrdinalIgnoreCase)), timeZone, byMonth);
            var rows = buckets.Select(bucket => new ReportRow(
            [
                bucket.Start.ToString(format, CultureInfo.InvariantCulture),
                ReportFormat.Number(bucket.Totals.Orders),
                Money(bucket.Totals.Total - bucket.Totals.Tax, currency),
                Money(bucket.Totals.Tax, currency),
            ])).ToList();

            rows.Add(new ReportRow([S["Total"].Value, ReportFormat.Number(totals.Orders), Money(taxed, currency), Money(totals.Tax, currency)], emphasize: true));

            document.Add(ReportSection.ForTable(S["Tax ({0})", currency].Value,
            [
                new ReportColumn(byMonth ? S["Month"].Value : S["Day"].Value),
                new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Charged before tax"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            ], rows));
        }

        return document;
    }
}

/// <summary>
/// The VAT one-stop-shop (OSS) return: the VAT charged on orders paid in the period that went to other EU countries, by
/// country and rate, with refunds taken off. A quarter's report is the return's figures; orders to the store's own
/// country (its tax origin) are declared at home and left out.
/// </summary>
public sealed class OrderVatOssReport : OrderReportBase, IReport
{
    private readonly ISiteService _siteService;

    public OrderVatOssReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderVatOssReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
        _siteService = siteService;
    }

    public static string Name => "order-vat-oss";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderVatOssReport>("VAT one-stop shop (OSS)");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderVatOssReport>("The VAT charged on orders to other EU countries in the period, by country and rate, refunds taken off: the figures of the EU one-stop-shop return. Run it for a quarter.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var origin = (await _siteService.GetSettingsAsync<CrestApps.OrchardCore.Taxation.Models.TaxOriginSettings>()).Origin?.Country;
        var document = new ReportDocument { Title = DisplayName.Value };
        var rows = OrderVatOss.Summarize(orders, origin);

        if (rows.Count == 0)
        {
            return document.Add(ReportSection.ForMetrics(S["No EU VAT"].Value,
            [
                new ReportMetric(S["Orders"].Value, ReportFormat.Number(0), S["No order paid in the period was charged VAT in another EU country."].Value),
            ]));
        }

        foreach (var currency in rows.Select(row => row.Currency).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var inCurrency = rows.Where(row => string.Equals(row.Currency, currency, StringComparison.OrdinalIgnoreCase)).ToList();
            var table = inCurrency.Select(row => new ReportRow(
            [
                row.Country,
                ReportFormat.Percent((double)row.Rate),
                ReportFormat.Number(row.Orders),
                Money(row.Taxable, currency),
                Money(row.Vat, currency),
            ])).ToList();

            table.Add(new ReportRow([S["Total"].Value, string.Empty, ReportFormat.Number(inCurrency.Sum(row => row.Orders)), Money(inCurrency.Sum(row => row.Taxable), currency), Money(inCurrency.Sum(row => row.Vat), currency)], emphasize: true));

            document.Add(ReportSection.ForMetrics(S["Summary ({0})", currency].Value,
            [
                new ReportMetric(S["VAT due"].Value, Money(inCurrency.Sum(row => row.Vat), currency), S["Refunds taken off."].Value),
                new ReportMetric(S["Countries"].Value, ReportFormat.Number(inCurrency.Select(row => row.Country).Distinct().Count())),
            ]));
            document.Add(ReportSection.ForTable(S["VAT by country and rate ({0})", currency].Value,
            [
                new ReportColumn(S["Country"].Value),
                new ReportColumn(S["Rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Taxable amount"].Value, ReportColumnAlign.End),
                new ReportColumn(S["VAT"].Value, ReportColumnAlign.End),
            ], table));
        }

        return document;
    }
}

/// <summary>
/// Adds up the VAT of orders to other EU countries for the one-stop-shop return.
/// </summary>
public static class OrderVatOss
{
    /// <summary>
    /// The EU member states, by their two-letter codes; Greece is GR here, as addresses write it.
    /// </summary>
    public static readonly IReadOnlySet<string> EuCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU",
        "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
    };

    /// <summary>
    /// Returns the VAT of the orders by the country they went to and the rate, in each currency: orders to EU countries
    /// other than <paramref name="originCountry"/>, with what was refunded taken off in proportion.
    /// </summary>
    public static IReadOnlyList<OrderVatOssRow> Summarize(IEnumerable<Order> orders, string originCountry)
    {
        var rows = new Dictionary<(string Country, decimal Rate, string Currency), (HashSet<string> Orders, decimal Taxable, decimal Vat)>();

        foreach (var order in orders ?? [])
        {
            var country = (order.ShippingAddress?.Country ?? order.BillingAddress?.Country)?.ToUpperInvariant();

            if (country is null || !EuCountries.Contains(country) || string.Equals(country, originCountry, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var total = order.Total ?? order.Subtotal;
            var kept = total > 0m ? Math.Clamp(1m - (order.RefundedAmount / total), 0m, 1m) : 1m;

            foreach (var line in (order.TaxLines ?? []).Where(line => string.Equals(line.TaxType, "VAT", StringComparison.OrdinalIgnoreCase) && line.TaxAmount != 0m))
            {
                var key = (country, line.Rate, order.Currency ?? string.Empty);
                var row = rows.TryGetValue(key, out var existing) ? existing : (new HashSet<string>(StringComparer.Ordinal), 0m, 0m);

                row.Item1.Add(order.ItemId);
                rows[key] = (row.Item1, row.Item2 + (line.TaxableAmount * kept), row.Item3 + (line.TaxAmount * kept));
            }
        }

        return [.. rows
            .Select(entry => new OrderVatOssRow(entry.Key.Country, entry.Key.Rate, entry.Key.Currency, entry.Value.Orders.Count, Payments.Money.Round(entry.Value.Taxable, entry.Key.Currency), Payments.Money.Round(entry.Value.Vat, entry.Key.Currency)))
            .OrderBy(row => row.Currency, StringComparer.Ordinal)
            .ThenBy(row => row.Country, StringComparer.Ordinal)
            .ThenByDescending(row => row.Rate)];
    }
}

/// <summary>
/// The VAT charged at one rate in one country, in one currency.
/// </summary>
public sealed record OrderVatOssRow(string Country, decimal Rate, string Currency, int Orders, decimal Taxable, decimal Vat);

/// <summary>
/// What each promotion code took off the orders paid in the period.
/// </summary>
public sealed class OrderDiscountsReport : OrderReportBase, IReport
{
    public OrderDiscountsReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderDiscountsReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-discounts";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderDiscountsReport>("Discounts on orders");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderDiscountsReport>("How many orders paid in the period used each promotion code, and what it took off.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from, to, cancellationToken);
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        var uses = OrderReportAggregator.ByDiscount(orders);

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Orders with a discount"].Value, ReportFormat.Number(orders.Count(order => order.DiscountTotal > 0m))),
            new ReportMetric(S["Orders"].Value, ReportFormat.Number(orders.Count)),
        ]));

        document.Add(ReportSection.ForTable(S["Codes"].Value,
        [
            new ReportColumn(S["Code"].Value),
            new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Taken off"].Value, ReportColumnAlign.End),
        ], [.. uses.Select(use => new ReportRow([use.Code, ReportFormat.Number(use.Orders), Money(use.Amount, use.Currency)]))]));

        return document;
    }
}

/// <summary>
/// The refunds given in the period.
/// </summary>
public sealed class OrderRefundsReport : OrderReportBase, IReport
{
    // A refund comes after the order: refunds of orders paid up to this long before the period are found.
    private static readonly TimeSpan _refundWindow = TimeSpan.FromDays(180);

    public OrderRefundsReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderRefundsReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-refunds";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderRefundsReport>("Refunds");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderRefundsReport>("The money given back in the period, refund by refund, for orders paid up to 180 days before it.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from - _refundWindow, to, cancellationToken);
        var timeZone = await GetTimeZoneAsync();
        var refunds = OrderReportAggregator.Refunds(orders, from, to);
        var document = new ReportDocument { Title = DisplayName.Value };

        var metrics = new List<ReportMetric> { new(S["Refunds"].Value, ReportFormat.Number(refunds.Count)) };

        foreach (var currency in refunds.GroupBy(entry => entry.Order.Currency ?? string.Empty))
        {
            metrics.Add(new ReportMetric(S["Given back ({0})", currency.Key].Value, Money(currency.Sum(entry => entry.Refund.Amount), currency.Key)));
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, metrics));

        document.Add(ReportSection.ForTable(S["Refunds"].Value,
        [
            new ReportColumn(S["Day"].Value),
            new ReportColumn(S["Order"].Value),
            new ReportColumn(S["Reason"].Value),
            new ReportColumn(S["Amount"].Value, ReportColumnAlign.End),
        ], [.. refunds.Select(entry => new ReportRow(
        [
            Day(entry.Refund.CreatedUtc, timeZone),
            entry.Order.OrderNumber,
            entry.Refund.Reason ?? string.Empty,
            Money(entry.Refund.Amount, entry.Order.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// The disputes, or chargebacks, payers opened in the period, with the ones still needing a response first.
/// </summary>
public sealed class OrderDisputesReport : OrderReportBase, IReport
{
    // A dispute comes after the payment: disputes of orders paid up to this long before the period are found.
    private static readonly TimeSpan _disputeWindow = TimeSpan.FromDays(180);

    public OrderDisputesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderDisputesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-disputes";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderDisputesReport>("Disputes");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderDisputesReport>("The payments disputed with the payer's bank in the period, open ones first, for orders paid up to 180 days before it.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var orders = await ReadPaidAsync(from - _disputeWindow, to, cancellationToken);
        var timeZone = await GetTimeZoneAsync();
        var disputes = orders
            .SelectMany(order => (order.Disputes ?? []).Select(dispute => (Order: order, Dispute: dispute)))
            .Where(entry => entry.Dispute.CreatedUtc >= from && entry.Dispute.CreatedUtc < to)
            .OrderBy(entry => entry.Dispute.IsClosed)
            .ThenByDescending(entry => entry.Dispute.CreatedUtc)
            .ToList();

        var document = new ReportDocument { Title = DisplayName.Value };
        var metrics = new List<ReportMetric>
        {
            new(S["Disputes"].Value, ReportFormat.Number(disputes.Count)),
            new(S["Still open"].Value, ReportFormat.Number(disputes.Count(entry => !entry.Dispute.IsClosed))),
            new(S["Won"].Value, ReportFormat.Number(disputes.Count(entry => entry.Dispute.Status == "won"))),
            new(S["Lost"].Value, ReportFormat.Number(disputes.Count(entry => entry.Dispute.Status == "lost"))),
        };

        foreach (var currency in disputes.GroupBy(entry => entry.Dispute.Currency ?? entry.Order.Currency ?? string.Empty))
        {
            metrics.Add(new ReportMetric(S["Disputed ({0})", currency.Key].Value, Money(currency.Sum(entry => entry.Dispute.Amount), currency.Key)));
        }

        document.Add(ReportSection.ForMetrics(S["Summary"].Value, metrics));

        document.Add(ReportSection.ForTable(S["Disputes"].Value,
        [
            new ReportColumn(S["Opened"].Value),
            new ReportColumn(S["Order"].Value),
            new ReportColumn(S["Why"].Value),
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Amount"].Value, ReportColumnAlign.End),
        ], [.. disputes.Select(entry => new ReportRow(
        [
            Day(entry.Dispute.CreatedUtc, timeZone),
            entry.Order.OrderNumber,
            Services.OrderDisputeText.Reason(S, entry.Dispute.Reason),
            Services.OrderDisputeText.Status(S, entry.Dispute.Status),
            Money(entry.Dispute.Amount, entry.Dispute.Currency ?? entry.Order.Currency),
        ]))]));

        return document;
    }
}

/// <summary>
/// Paid orders not shipped in full yet, oldest first.
/// </summary>
public sealed class UnfulfilledOrdersReport : OrderReportBase, IReport
{
    public UnfulfilledOrdersReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<UnfulfilledOrdersReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "unfulfilled-orders";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<UnfulfilledOrdersReport>("Orders to ship");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<UnfulfilledOrdersReport>("Paid orders that haven't shipped in full, oldest first, whatever the period.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var orders = (await ReadUnfulfilledAsync(cancellationToken)).OrderBy(order => order.PaidUtc ?? order.CreatedUtc).ToList();
        var timeZone = await GetTimeZoneAsync();
        var now = UtcNow;
        var document = new ReportDocument { Title = DisplayName.Value };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Orders to ship"].Value, ReportFormat.Number(orders.Count)),
            new ReportMetric(S["Partly shipped"].Value, ReportFormat.Number(orders.Count(order => order.FulfillmentStatus == OrderFulfillmentStatus.PartiallyFulfilled))),
            new ReportMetric(S["Waiting over 2 days"].Value, ReportFormat.Number(orders.Count(order => now - (order.PaidUtc ?? order.CreatedUtc) > TimeSpan.FromDays(2)))),
        ]));

        document.Add(ReportSection.ForTable(S["Orders"].Value,
        [
            new ReportColumn(S["Order"].Value),
            new ReportColumn(S["Paid"].Value),
            new ReportColumn(S["Days waiting"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Items"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Shipped"].Value),
            new ReportColumn(S["Customer"].Value),
        ], [.. orders.Select(order => new ReportRow(
        [
            order.OrderNumber,
            Day(order.PaidUtc ?? order.CreatedUtc, timeZone),
            ReportFormat.Number((int)Math.Floor((now - (order.PaidUtc ?? order.CreatedUtc)).TotalDays)),
            ReportFormat.Number(order.ItemCount),
            order.FulfillmentStatus == OrderFulfillmentStatus.PartiallyFulfilled ? S["Partly"].Value : S["Not yet"].Value,
            order.CustomerEmail ?? string.Empty,
        ]))]));

        return document;
    }
}
