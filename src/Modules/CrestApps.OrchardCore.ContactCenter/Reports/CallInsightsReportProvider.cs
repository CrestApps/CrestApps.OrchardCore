using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// The Contact Center call insights report: inbound/outbound volume, outcomes, handle time, channel and
/// status breakdowns, and a daily trend.
/// </summary>
public sealed class CallInsightsReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallInsightsReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallInsightsReportProvider(
        IContactCenterReportingService reportingService,
        IStringLocalizer<CallInsightsReportProvider> stringLocalizer)
        : base(reportingService, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-call-insights";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Call insights"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Inbound and outbound call volume, answered, abandoned, and failed calls, handle time, and a daily trend."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetCallInsightsAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var document = new ReportDocument();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Total"].Value, ReportFormat.Number(report.Total)),
            new ReportMetric(S["Inbound"].Value, ReportFormat.Number(report.Inbound)),
            new ReportMetric(S["Outbound"].Value, ReportFormat.Number(report.Outbound)),
            new ReportMetric(S["Answered"].Value, ReportFormat.Number(report.Answered), ReportFormat.Percent(report.AnswerRate)),
            new ReportMetric(S["Abandoned"].Value, ReportFormat.Number(report.Abandoned), ReportFormat.Percent(report.AbandonmentRate)),
            new ReportMetric(S["Failed"].Value, ReportFormat.Number(report.Failed)),
            new ReportMetric(S["Avg handle time"].Value, ReportFormat.Duration(report.AverageHandleTimeSeconds)),
            new ReportMetric(S["Avg speed of answer"].Value, ReportFormat.Duration(report.AverageSpeedOfAnswerSeconds)),
            new ReportMetric(S["Total talk time"].Value, ReportFormat.Duration(report.TotalTalkTimeSeconds)),
            new ReportMetric(S["Total wrap-up time"].Value, ReportFormat.Duration(report.TotalWrapUpTimeSeconds)),
        ]));

        if (report.ByChannel.Count > 0)
        {
            var max = report.ByChannel.Max(entry => entry.Count);

            document.Add(ReportSection.ForBars(S["By channel"].Value,
                report.ByChannel.Select(entry => new ReportBar(entry.Label, ReportFormat.Number(entry.Count), max > 0 ? (double)entry.Count / max : 0))));
        }

        if (report.ByStatus.Count > 0)
        {
            var max = report.ByStatus.Max(entry => entry.Count);

            document.Add(ReportSection.ForBars(S["By status"].Value,
                report.ByStatus.Select(entry => new ReportBar(entry.Label, ReportFormat.Number(entry.Count), max > 0 ? (double)entry.Count / max : 0))));
        }

        if (report.Daily.Count > 0)
        {
            var columns = new[]
            {
                new ReportColumn(S["Date"].Value),
                new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
            };

            var rows = report.Daily.Select(point => new ReportRow(
            [
                point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReportFormat.Number(point.Total),
                ReportFormat.Number(point.Answered),
                ReportFormat.Number(point.Abandoned),
            ]));

            document.Add(ReportSection.ForTable(S["Daily volume"].Value, columns, rows));
        }

        return document;
    }
}
