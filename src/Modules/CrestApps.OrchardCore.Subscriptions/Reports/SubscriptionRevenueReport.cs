using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// The subscription revenue report: gross revenue, transaction volume, average value, and tax collected
/// for the reporting period, with a monthly revenue trend.
/// </summary>
public sealed class SubscriptionRevenueReport : SubscriptionReportBase
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRevenueReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionRevenueReport(
        ISession session,
        ISiteService siteService,
        IStringLocalizer<SubscriptionRevenueReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public override string Name => "subscription-revenue";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Subscription revenue"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Gross revenue, transaction volume, average transaction value, and tax collected, with a monthly revenue trend."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var transactions = await _session.QueryIndex<SubscriptionTransactionIndex>().ListAsync(cancellationToken);
        var succeeded = SubscriptionReportAggregator.GetSucceededTransactions(transactions, range.FromUtc, range.ToUtc);
        var summary = SubscriptionReportAggregator.SummarizeRevenue(succeeded);
        var monthly = SubscriptionReportAggregator.BucketRevenueByMonth(succeeded);
        var currency = await GetCurrencyAsync();

        var document = new ReportDocument
        {
            Title = DisplayName.Value,
        };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Total revenue"].Value, FormatCurrency(summary.TotalRevenue, currency)),
            new ReportMetric(S["Transactions"].Value, ReportFormat.Number(summary.TransactionCount)),
            new ReportMetric(S["Average transaction value"].Value, FormatCurrency(summary.AverageTransactionValue, currency)),
            new ReportMetric(S["Tax collected"].Value, FormatCurrency(summary.TotalTax, currency)),
        ]));

        if (monthly.Count > 0)
        {
            var chart = new ReportChart
            {
                Type = ReportChartType.Bar,
                Labels = [.. monthly.Select(bucket => bucket.MonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture))],
                Datasets =
                [
                    new ReportChartDataset(S["Revenue"].Value, monthly.Select(bucket => bucket.Revenue)),
                ],
            };

            document.Add(ReportSection.ForChart(S["Monthly revenue"].Value, chart, 12));

            var columns = new[]
            {
                new ReportColumn(S["Month"].Value),
                new ReportColumn(S["Transactions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Revenue"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tax"].Value, ReportColumnAlign.End),
            };

            var rows = new List<ReportRow>();

            foreach (var bucket in monthly)
            {
                rows.Add(new ReportRow(
                [
                    bucket.MonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    ReportFormat.Number(bucket.TransactionCount),
                    FormatCurrency(bucket.Revenue, currency),
                    FormatCurrency(bucket.Tax, currency),
                ]));
            }

            rows.Add(new ReportRow(
            [
                S["Total"].Value,
                ReportFormat.Number(summary.TransactionCount),
                FormatCurrency(summary.TotalRevenue, currency),
                FormatCurrency(summary.TotalTax, currency),
            ], emphasize: true));

            document.Add(ReportSection.ForTable(S["Revenue by month"].Value, columns, rows));
        }

        return document;
    }
}
