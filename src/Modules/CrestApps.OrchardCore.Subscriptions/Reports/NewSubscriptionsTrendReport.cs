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
/// The new subscriptions trend report: a monthly chart of the number of subscriptions started over the
/// reporting period.
/// </summary>
public sealed class NewSubscriptionsTrendReport : SubscriptionReportBase
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewSubscriptionsTrendReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public NewSubscriptionsTrendReport(
        ISession session,
        ISiteService siteService,
        IStringLocalizer<NewSubscriptionsTrendReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public override string Name => "new-subscriptions-trend";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["New subscriptions trend"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["The number of new subscriptions started each month over the reporting period."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.Executive;

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var subscriptions = await _session.QueryIndex<SubscriptionIndex>().ListAsync(cancellationToken);
        var monthly = SubscriptionReportAggregator.BucketNewSubscriptionsByMonth(subscriptions, range.FromUtc, range.ToUtc);

        var document = new ReportDocument
        {
            Title = DisplayName.Value,
        };

        if (monthly.Count > 0)
        {
            var chart = new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = [.. monthly.Select(bucket => bucket.MonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture))],
                Datasets =
                [
                    new ReportChartDataset(S["New subscriptions"].Value, monthly.Select(bucket => (double)bucket.Count)),
                ],
            };

            document.Add(ReportSection.ForChart(S["New subscriptions by month"].Value, chart, 12));

            var columns = new[]
            {
                new ReportColumn(S["Month"].Value),
                new ReportColumn(S["New subscriptions"].Value, ReportColumnAlign.End),
            };

            var rows = monthly.Select(bucket => new ReportRow(
            [
                bucket.MonthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                ReportFormat.Number(bucket.Count),
            ]));

            document.Add(ReportSection.ForTable(S["New subscriptions by month"].Value, columns, rows));
        }

        return document;
    }
}
