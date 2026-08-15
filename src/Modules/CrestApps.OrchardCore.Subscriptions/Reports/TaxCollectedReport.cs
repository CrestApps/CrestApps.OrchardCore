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
/// The tax collected report: total tax and taxable transaction volume for the reporting period, with a
/// monthly breakdown of tax collected.
/// </summary>
public sealed class TaxCollectedReport : SubscriptionReportBase
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCollectedReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxCollectedReport(
        ISession session,
        ISiteService siteService,
        IStringLocalizer<TaxCollectedReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public override string Name => "tax-collected";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Tax collected"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Total tax collected and taxable transaction volume, with a monthly breakdown."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.BillingUsage;

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
            new ReportMetric(S["Total tax collected"].Value, FormatCurrency(summary.TotalTax, currency)),
            new ReportMetric(S["Taxable transactions"].Value, ReportFormat.Number(summary.TransactionCount)),
        ]));

        if (monthly.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Month"].Value),
                new ReportColumn(S["Transactions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tax collected"].Value, ReportColumnAlign.End),
            };

            var rows = new List<ReportRow>();

            foreach (var bucket in monthly)
            {
                rows.Add(new ReportRow(
                [
                    bucket.MonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    ReportFormat.Number(bucket.TransactionCount),
                    FormatCurrency(bucket.Tax, currency),
                ]));
            }

            rows.Add(new ReportRow(
            [
                S["Total"].Value,
                ReportFormat.Number(summary.TransactionCount),
                FormatCurrency(summary.TotalTax, currency),
            ], emphasize: true));

            document.Add(ReportSection.ForTable(S["Tax collected by month"].Value, columns, rows));
        }

        return document;
    }
}
