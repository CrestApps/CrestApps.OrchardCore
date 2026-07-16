using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The CRM activity summary report: activity volume and completion, broken down by source, channel, and
/// status, with a daily created-activity trend.
/// </summary>
public sealed class ActivitySummaryReportProvider : OmnichannelReportBase
{
    private readonly ISession _session;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitySummaryReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivitySummaryReportProvider(
        ISession session,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IStringLocalizer<ActivitySummaryReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _campaignManager = campaignManager;
    }

    /// <inheritdoc/>
    public override string Name => "omnichannel-activity-summary";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Activity summary"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["CRM activity volume and completion, broken down by source, channel, and status, with a daily trend."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.Operations;

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var activities = await OmnichannelReportQuery.GetCreatedAsync(
            _session,
            context.FromUtc,
            context.ToUtc,
            await OmnichannelReportFilter.GetCriteriaAsync(context.Filter, _campaignManager, cancellationToken),
            cancellationToken);
        var data = OmnichannelReportAggregator.BuildActivitySummary(activities);

        var document = new ReportDocument();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Total"].Value, ReportFormat.Number(data.Counts.Total)),
            new ReportMetric(S["Completed"].Value, ReportFormat.Number(data.Counts.Completed), ReportFormat.Percent(data.Counts.CompletionRate)),
            new ReportMetric(S["Pending"].Value, ReportFormat.Number(data.Counts.Pending)),
            new ReportMetric(S["In progress"].Value, ReportFormat.Number(data.Counts.InProgress)),
            new ReportMetric(S["Failed"].Value, ReportFormat.Number(data.Counts.Failed)),
            new ReportMetric(S["Cancelled"].Value, ReportFormat.Number(data.Counts.Cancelled)),
        ]));

        AddBreakdown(document, S["By source"].Value, data.BySource);
        AddBreakdown(document, S["By channel"].Value, data.ByChannel);
        AddBreakdown(document, S["By status"].Value, data.ByStatus);

        if (data.Daily.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Date"].Value),
                new ReportColumn(S["Created"].Value, ReportColumnAlign.End),
            };

            var rows = data.Daily.Select(point => new ReportRow(
            [
                point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReportFormat.Number(point.Count),
            ]));

            document.Add(ReportSection.ForTable(S["Daily created"].Value, columns, rows));
        }

        return document;
    }

    private static void AddBreakdown(ReportDocument document, string title, IReadOnlyDictionary<string, long> counts)
    {
        if (counts.Count == 0)
        {
            return;
        }

        var max = counts.Values.Max();

        document.Add(ReportSection.ForBars(title, counts
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ReportBar(entry.Key, ReportFormat.Number(entry.Value), max > 0 ? (double)entry.Value / max : 0))));
    }
}
