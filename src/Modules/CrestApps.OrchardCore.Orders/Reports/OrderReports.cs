using System.Globalization;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Reports;

/// <summary>
/// What the order reports share: the orders paid in the chosen period, the site's time zone for days, and money in each
/// order's own currency.
/// </summary>
public abstract class OrderReportBase : IReport
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

    public abstract string Name { get; }

    public abstract LocalizedString DisplayName { get; }

    public abstract LocalizedString Description { get; }

    public virtual string Category
        => ReportsConstants.Categories.Commerce;

    public virtual Permission Permission
        => OrderPermissions.ManageOrders;

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
public sealed class OrderSalesReport : OrderReportBase
{
    public OrderSalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderSalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-sales";

    public override LocalizedString DisplayName => S["Sales"];

    public override LocalizedString Description => S["What the orders paid in the period came to, with discounts, shipping, tax and refunds, day by day."];

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
public sealed class OrderProductSalesReport : OrderReportBase
{
    public OrderProductSalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderProductSalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-product-sales";

    private string KindName(ProductType productType)
        => productType switch
        {
            ProductType.Good => S["Goods"].Value,
            ProductType.Digital => S["Digital"].Value,
            ProductType.Service => S["Services"].Value,
            _ => S["Other"].Value,
        };

    public override LocalizedString DisplayName => S["Sales by product"];

    public override LocalizedString Description => S["The units each product and variant sold in the period, and what they sold for before discounts."];

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
public sealed class OrderCountrySalesReport : OrderReportBase
{
    public OrderCountrySalesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderCountrySalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-country-sales";

    public override LocalizedString DisplayName => S["Sales by country"];

    public override LocalizedString Description => S["Orders paid in the period and what they came to, by the country they ship to, or are billed to when nothing ships."];

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
public sealed class OrderTaxReport : OrderReportBase
{
    public OrderTaxReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderTaxReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-tax";

    public override LocalizedString DisplayName => S["Tax on orders"];

    public override LocalizedString Description => S["The tax charged on the orders paid in the period, and what it was charged on, day by day."];

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
/// What each promotion code took off the orders paid in the period.
/// </summary>
public sealed class OrderDiscountsReport : OrderReportBase
{
    public OrderDiscountsReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderDiscountsReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-discounts";

    public override LocalizedString DisplayName => S["Discounts on orders"];

    public override LocalizedString Description => S["How many orders paid in the period used each promotion code, and what it took off."];

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
public sealed class OrderRefundsReport : OrderReportBase
{
    // A refund comes after the order: refunds of orders paid up to this long before the period are found.
    private static readonly TimeSpan _refundWindow = TimeSpan.FromDays(180);

    public OrderRefundsReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderRefundsReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-refunds";

    public override LocalizedString DisplayName => S["Refunds"];

    public override LocalizedString Description => S["The money given back in the period, refund by refund, for orders paid up to 180 days before it."];

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
public sealed class OrderDisputesReport : OrderReportBase
{
    // A dispute comes after the payment: disputes of orders paid up to this long before the period are found.
    private static readonly TimeSpan _disputeWindow = TimeSpan.FromDays(180);

    public OrderDisputesReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderDisputesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "order-disputes";

    public override LocalizedString DisplayName => S["Disputes"];

    public override LocalizedString Description => S["The payments disputed with the payer's bank in the period, open ones first, for orders paid up to 180 days before it."];

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
public sealed class UnfulfilledOrdersReport : OrderReportBase
{
    public UnfulfilledOrdersReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<UnfulfilledOrdersReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public override string Name => "unfulfilled-orders";

    public override LocalizedString DisplayName => S["Orders to ship"];

    public override LocalizedString Description => S["Paid orders that haven't shipped in full, oldest first, whatever the period."];

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
