using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// The product performance report: succeeded transactions grouped by product (content type) into a
/// revenue and tax table, with a top-products-by-revenue bar breakdown.
/// </summary>
public sealed class ProductPerformanceReport : SubscriptionReportBase
{
    private const int TopProductCount = 10;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductPerformanceReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ProductPerformanceReport(
        ISession session,
        ISiteService siteService,
        IStringLocalizer<ProductPerformanceReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public override string Name => "product-performance";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Product performance"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Revenue, transaction volume, and tax grouped by subscription product, highlighting the top products by revenue."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.BillingUsage;

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var transactions = await _session.QueryIndex<SubscriptionTransactionIndex>().ListAsync(cancellationToken);
        var succeeded = SubscriptionReportAggregator.GetSucceededTransactions(transactions, range.FromUtc, range.ToUtc);
        var products = SubscriptionReportAggregator.GroupByProduct(succeeded);
        var currency = await GetCurrencyAsync();

        var document = new ReportDocument
        {
            Title = DisplayName.Value,
        };

        if (products.Count > 0)
        {
            var maxRevenue = products.Max(product => product.GrossRevenue);

            document.Add(ReportSection.ForBars(S["Top products by revenue"].Value, products
                .Take(TopProductCount)
                .Select(product => new ReportBar(
                    ProductLabel(product.ContentType),
                    FormatCurrency(product.GrossRevenue, currency),
                    maxRevenue > 0 ? product.GrossRevenue / maxRevenue : 0))));

            var columns = new[]
            {
                new ReportColumn(S["Product"].Value),
                new ReportColumn(S["Transactions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Gross revenue"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            };

            var rows = new List<ReportRow>();

            foreach (var product in products)
            {
                rows.Add(new ReportRow(
                [
                    ProductLabel(product.ContentType),
                    ReportFormat.Number(product.TransactionCount),
                    FormatCurrency(product.GrossRevenue, currency),
                    FormatCurrency(product.Tax, currency),
                ]));
            }

            rows.Add(new ReportRow(
            [
                S["Total"].Value,
                ReportFormat.Number(products.Sum(product => product.TransactionCount)),
                FormatCurrency(products.Sum(product => product.GrossRevenue), currency),
                FormatCurrency(products.Sum(product => product.Tax), currency),
            ], emphasize: true));

            document.Add(ReportSection.ForTable(S["Revenue by product"].Value, columns, rows));
        }

        return document;
    }

    private string ProductLabel(string contentType)
    {
        return string.IsNullOrEmpty(contentType)
            ? S["Unspecified"].Value
            : contentType;
    }
}
