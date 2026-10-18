using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.Reports;

/// <summary>
/// What each category sold in the period. A product in two categories counts in both.
/// </summary>
public sealed class OrderCategorySalesReport : OrderReportBase, IReport
{
    private readonly ProductCategoryNames _categoryNames;

    public OrderCategorySalesReport(OrderReportReader reader, ProductCategoryNames categoryNames, ISiteService siteService, IClock clock, IStringLocalizer<OrderCategorySalesReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
        _categoryNames = categoryNames;
    }

    public static string Name => "order-category-sales";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderCategorySalesReport>("Sales by category");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderCategorySalesReport>("The orders, units and sales of each category in the period, with a table for each taxonomy products are sorted by, such as categories and brands. A product in two categories counts in both.");

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

        var terms = await _categoryNames.GetAsync(cancellationToken);
        var categories = OrderReportAggregator.ByCategory(orders, termId => terms.TryGetValue(termId, out var term) ? term.Taxonomy : null);

        if (categories.Count == 0)
        {
            return document.Add(ReportSection.ForMetrics(S["Summary"].Value, [new ReportMetric(S["Categories"].Value, "0", S["None of the products sold is in a category."].Value)]));
        }

        string NameOf(CategorySales category)
            => string.IsNullOrEmpty(category.CategoryId) ? S["Not in any"].Value
                : terms.TryGetValue(category.CategoryId, out var term) ? term.Path
                : S["Removed"].Value;

        foreach (var group in categories.GroupBy(category => (category.Taxonomy, category.Currency)))
        {
            var taxonomy = string.IsNullOrEmpty(group.Key.Taxonomy) ? S["Removed taxonomy"].Value : group.Key.Taxonomy;
            var named = group.Where(category => !string.IsNullOrEmpty(category.CategoryId)).Take(10).ToArray();
            var most = named.Length == 0 ? 1m : Math.Max(1m, named.Max(category => category.Sales));

            if (named.Length > 0)
            {
                document.Add(ReportSection.ForBars(S["Top by sales: {0} ({1})", taxonomy, group.Key.Currency].Value,
                    [.. named.Select(category => new ReportBar(NameOf(category), Money(category.Sales, category.Currency), (double)(category.Sales / most)))]));
            }

            document.Add(ReportSection.ForTable(S["{0} ({1})", taxonomy, group.Key.Currency].Value,
            [
                new ReportColumn(taxonomy),
                new ReportColumn(S["Orders"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Units"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Sales"].Value, ReportColumnAlign.End),
            ], [.. group.Select(category => new ReportRow(
            [
                NameOf(category),
                ReportFormat.Number(category.Orders),
                ReportFormat.Number(category.Units),
                Money(category.Sales, category.Currency),
            ]))]));
        }

        return document;
    }
}

/// <summary>
/// Who bought in the period: first-time and returning buyers, buyers who came back for more, and what each has spent in
/// all.
/// </summary>
public sealed class OrderCustomersReport : OrderReportBase, IReport
{
    private const int TopBuyers = 25;

    // Lifetime spend counts every paid order up to the period's end.
    private static readonly DateTime _beginning = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public OrderCustomersReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderCustomersReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-customers";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderCustomersReport>("Customers");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderCustomersReport>("Who bought in the period: first-time and returning buyers, how many have bought more than once, and what the top buyers have spent in all.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var history = await ReadPaidAsync(_beginning, to, cancellationToken);
        var orders = history.Where(order => order.PaidUtc >= from).ToList();
        var timeZone = await GetTimeZoneAsync();
        var document = new ReportDocument { Title = DisplayName.Value };

        if (orders.Count == 0)
        {
            return document.Add(NoOrders());
        }

        foreach (var purchases in OrderReportAggregator.Customers(orders, history, from))
        {
            var currency = purchases.Currency;
            var buyers = purchases.Buyers;

            document.Add(ReportSection.ForMetrics(S["Summary ({0})", currency].Value,
            [
                new ReportMetric(S["Buyers"].Value, ReportFormat.Number(buyers.Count)),
                new ReportMetric(S["First-time buyers"].Value, ReportFormat.Number(purchases.NewBuyers), S["Their first paid order was in this period."].Value),
                new ReportMetric(S["Returning buyers"].Value, ReportFormat.Number(purchases.ReturningBuyers), S["They had bought before this period."].Value),
                new ReportMetric(S["Repeat buyers"].Value, Percent(purchases.RepeatBuyers, buyers.Count), S["{0} of them have paid for more than one order.", ReportFormat.Number(purchases.RepeatBuyers)].Value),
                new ReportMetric(S["Average lifetime spend"].Value, Money(purchases.AverageLifetimeSpent, currency), S["What these buyers have spent in all, less refunds, on average."].Value),
            ]));

            document.Add(ReportSection.ForChart(S["First-time and returning ({0})", currency].Value, new ReportChart
            {
                Type = ReportChartType.Doughnut,
                Labels = [S["First-time"].Value, S["Returning"].Value],
                Datasets = [new ReportChartDataset(S["Buyers"].Value, [purchases.NewBuyers, purchases.ReturningBuyers])],
                ShowLegend = true,
            }, 4));

            document.Add(ReportSection.ForTable(S["Top buyers ({0})", currency].Value,
            [
                new ReportColumn(S["Buyer"].Value),
                new ReportColumn(S["Email"].Value),
                new ReportColumn(S["Orders in period"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Spent in period"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Orders in all"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Spent in all"].Value, ReportColumnAlign.End),
                new ReportColumn(S["First order"].Value),
            ], [.. buyers.Take(TopBuyers).Select(buyer => new ReportRow(
            [
                string.IsNullOrEmpty(buyer.Name) ? S["Guest"].Value : buyer.Name,
                buyer.Email ?? string.Empty,
                ReportFormat.Number(buyer.OrdersInPeriod),
                Money(buyer.SpentInPeriod, currency),
                ReportFormat.Number(buyer.LifetimeOrders),
                Money(buyer.LifetimeSpent, currency),
                Day(buyer.FirstPaidUtc, timeZone),
            ]))]));
        }

        return document;
    }

    private static string Percent(int part, int whole)
        => whole == 0 ? "0%" : ((double)part / whole).ToString("P0", CultureInfo.CurrentCulture);
}

/// <summary>
/// The order checkouts started in the period and how they ended: paid, failed, left or still open.
/// </summary>
public sealed class CheckoutConversionReport : OrderReportBase, IReport
{
    private const int PageSize = 1000;

    private readonly ISession _session;

    public CheckoutConversionReport(OrderReportReader reader, ISession session, ISiteService siteService, IClock clock, IStringLocalizer<CheckoutConversionReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
        _session = session;
    }

    public static string Name => "checkout-conversion";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CheckoutConversionReport>("Checkouts");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<CheckoutConversionReport>("The order checkouts started in the period and how they ended: paid, failed, left or expired, or still open, day by day.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var timeZone = await GetTimeZoneAsync();
        var checkouts = new List<CheckoutSessionIndex>();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session
                .QueryIndex<CheckoutSessionIndex>(index => index.ReferenceType == CheckoutReferenceTypes.Order && index.CreatedUtc >= from && index.CreatedUtc < to)
                .OrderBy(index => index.Id)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken)).ToList();

            checkouts.AddRange(page);

            if (page.Count < PageSize)
            {
                break;
            }
        }

        var document = new ReportDocument { Title = DisplayName.Value };
        var funnel = OrderReportAggregator.Funnel(checkouts.Select(checkout => checkout.Status));

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Checkouts started"].Value, ReportFormat.Number(funnel.Started)),
            new ReportMetric(S["Paid"].Value, ReportFormat.Number(funnel.Paid)),
            new ReportMetric(S["Paid rate"].Value, funnel.PaidRate.ToString("P0", CultureInfo.CurrentCulture), S["The checkouts started that were paid."].Value),
            new ReportMetric(S["Left or expired"].Value, ReportFormat.Number(funnel.Abandoned)),
            new ReportMetric(S["Payment failed"].Value, ReportFormat.Number(funnel.Failed)),
            new ReportMetric(S["Still open"].Value, ReportFormat.Number(funnel.Open), S["Still in progress, or waiting for the payment provider."].Value),
        ]));

        if (funnel.Started == 0)
        {
            return document;
        }

        var byMonth = (to - from).TotalDays > 62;
        var format = byMonth ? "yyyy-MM" : "yyyy-MM-dd";
        var days = checkouts
            .GroupBy(checkout =>
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(checkout.CreatedUtc, DateTimeKind.Utc), timeZone);

                return byMonth ? new DateOnly(local.Year, local.Month, 1) : DateOnly.FromDateTime(local);
            })
            .OrderBy(group => group.Key)
            .Select(group => (Label: group.Key.ToString(format, CultureInfo.InvariantCulture), Funnel: OrderReportAggregator.Funnel(group.Select(checkout => checkout.Status))))
            .ToList();

        document.Add(ReportSection.ForChart(byMonth ? S["Checkouts by month"].Value : S["Checkouts by day"].Value, new ReportChart
        {
            Type = ReportChartType.Bar,
            Labels = [.. days.Select(day => day.Label)],
            Datasets =
            [
                new ReportChartDataset(S["Started"].Value, days.Select(day => (double)day.Funnel.Started)),
                new ReportChartDataset(S["Paid"].Value, days.Select(day => (double)day.Funnel.Paid)),
            ],
            ShowLegend = true,
        }, 12));

        document.Add(ReportSection.ForTable(byMonth ? S["By month"].Value : S["By day"].Value,
        [
            new ReportColumn(byMonth ? S["Month"].Value : S["Day"].Value),
            new ReportColumn(S["Started"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Paid"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Paid rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Left or expired"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Payment failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Still open"].Value, ReportColumnAlign.End),
        ], [.. days.Select(day => new ReportRow(
        [
            day.Label,
            ReportFormat.Number(day.Funnel.Started),
            ReportFormat.Number(day.Funnel.Paid),
            day.Funnel.PaidRate.ToString("P0", CultureInfo.CurrentCulture),
            ReportFormat.Number(day.Funnel.Abandoned),
            ReportFormat.Number(day.Funnel.Failed),
            ReportFormat.Number(day.Funnel.Open),
        ]))]));

        return document;
    }
}

/// <summary>
/// Names the categories orders were bought in: the terms of the site's published taxonomies, each with its taxonomy and
/// its path from the top, such as <c>Accessories › Watches</c>.
/// </summary>
public sealed class ProductCategoryNames
{
    private readonly ISession _session;

    public ProductCategoryNames(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Returns each term by its content item id.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, CategoryTerm>> GetAsync(CancellationToken cancellationToken = default)
    {
        var taxonomies = await _session
            .Query<ContentItem, ContentItemIndex>(index => index.ContentType == "Taxonomy" && index.Published)
            .ListAsync(cancellationToken);

        var terms = new Dictionary<string, CategoryTerm>(StringComparer.Ordinal);

        foreach (var taxonomy in taxonomies)
        {
            var root = JObject.FromObject(taxonomy);

            foreach (var term in TermsOf(root))
            {
                Collect(term, taxonomy.DisplayText ?? string.Empty, null, terms);
            }
        }

        return terms;
    }

    // A taxonomy's terms are under TaxonomyPart.Terms, and a term's children under its own Terms.
    private static IEnumerable<JsonObject> TermsOf(JsonObject item)
    {
        var terms = item["TaxonomyPart"]?["Terms"] as JsonArray ?? item["Terms"] as JsonArray;

        return terms?.OfType<JsonObject>() ?? [];
    }

    private static void Collect(JsonObject term, string taxonomy, string parentPath, Dictionary<string, CategoryTerm> terms)
    {
        if (term["ContentItemId"] is not JsonValue id || !id.TryGetValue<string>(out var contentItemId))
        {
            return;
        }

        var name = term["DisplayText"] is JsonValue text && text.TryGetValue<string>(out var displayText) && !string.IsNullOrEmpty(displayText)
            ? displayText
            : contentItemId;
        var path = string.IsNullOrEmpty(parentPath) ? name : parentPath + " \u203a " + name;

        terms.TryAdd(contentItemId, new CategoryTerm(taxonomy, name, path));

        foreach (var child in TermsOf(term))
        {
            Collect(child, taxonomy, path, terms);
        }
    }
}

/// <summary>
/// A taxonomy term: its taxonomy, its name and its path from the top.
/// </summary>
public sealed record CategoryTerm(string Taxonomy, string Name, string Path);
