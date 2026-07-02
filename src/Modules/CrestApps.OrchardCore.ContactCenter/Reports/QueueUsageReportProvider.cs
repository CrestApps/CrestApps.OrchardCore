using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// The Contact Center queue usage report: per-queue handled volume, outcomes, and live waiting depth.
/// </summary>
public sealed class QueueUsageReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueUsageReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueUsageReportProvider(
        IContactCenterReportingService reportingService,
        IStringLocalizer<QueueUsageReportProvider> stringLocalizer)
        : base(reportingService, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-queue-usage";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Queue usage"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per-queue handled, answered, and abandoned volume, handle time, and current waiting depth."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetQueueUsageAsync(context.FromUtc, context.ToUtc, cancellationToken);

        var noQueue = S["(No queue)"].Value;

        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Handled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg speed of answer"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Waiting now"].Value, ReportColumnAlign.End),
            new ReportColumn(S["SLA threshold"].Value, ReportColumnAlign.End),
        };

        var rows = report.Rows.Select(row => new ReportRow(
        [
            string.IsNullOrEmpty(row.QueueName) ? noQueue : row.QueueName,
            ReportFormat.Number(row.InteractionsHandled),
            ReportFormat.Number(row.Answered),
            ReportFormat.Number(row.Abandoned),
            ReportFormat.Duration(row.AverageHandleTimeSeconds),
            ReportFormat.Duration(row.AverageSpeedOfAnswerSeconds),
            ReportFormat.Number(row.CurrentWaiting),
            row.SlaThresholdSeconds > 0 ? ReportFormat.Duration(row.SlaThresholdSeconds) : "—",
        ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queues"].Value, columns, rows));
    }
}
