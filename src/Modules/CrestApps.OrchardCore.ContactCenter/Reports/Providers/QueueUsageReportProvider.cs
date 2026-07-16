using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

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
    public override string Category => ReportsConstants.Categories.QueueRouting;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.QueueGroupId,
        ContactCenterReportFilter.QueueId,
        ContactCenterReportFilter.Channel,
        ContactCenterReportFilter.Direction,
    ];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetQueueUsageAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

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

        var rows = report.Rows
            .Select(row => new ReportRow(
            [
                string.IsNullOrEmpty(row.QueueName) ? noQueue : row.QueueName,
                ReportFormat.Number(row.InteractionsHandled),
                ReportFormat.Number(row.Answered),
                ReportFormat.Number(row.Abandoned),
                ReportFormat.Duration(row.AverageHandleTimeSeconds),
                ReportFormat.Duration(row.AverageSpeedOfAnswerSeconds),
                ReportFormat.Number(row.CurrentWaiting),
                row.SlaThresholdSeconds > 0 ? ReportFormat.Duration(row.SlaThresholdSeconds) : "—",
            ]))
            .ToList();

        rows.Add(CreateGrandTotalRow(report.Totals, S["All queues"].Value, includeSlaColumn: true));

        var groupColumns = columns.Take(columns.Length - 1).ToArray();
        groupColumns[0] = new ReportColumn(S["Queue group"].Value);
        var noQueueGroup = S["(No queue group)"].Value;
        var unknownQueueGroup = S["(Unknown queue group)"].Value;
        var groupRows = report.GroupRows
            .Select(row => new ReportRow(
            [
                string.IsNullOrEmpty(row.QueueGroupId)
                    ? noQueueGroup
                    : string.IsNullOrEmpty(row.QueueGroupName)
                        ? unknownQueueGroup
                        : row.QueueGroupName,
                ReportFormat.Number(row.InteractionsHandled),
                ReportFormat.Number(row.Answered),
                ReportFormat.Number(row.Abandoned),
                ReportFormat.Duration(row.AverageHandleTimeSeconds),
                ReportFormat.Duration(row.AverageSpeedOfAnswerSeconds),
                ReportFormat.Number(row.CurrentWaiting),
            ], ReportRowKind.Subtotal))
            .ToList();

        groupRows.Add(CreateGrandTotalRow(report.Totals, S["All queue groups"].Value, includeSlaColumn: false));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queues"].Value, columns, rows))
            .Add(ReportSection.ForTable(S["Queue groups"].Value, groupColumns, groupRows));
    }

    internal static ReportRow CreateGrandTotalRow(
        QueueUsageTotals totals,
        string label,
        bool includeSlaColumn)
    {
        var cells = new List<string>
        {
            label,
            ReportFormat.Number(totals.InteractionsHandled),
            ReportFormat.Number(totals.Answered),
            ReportFormat.Number(totals.Abandoned),
            ReportFormat.Duration(totals.AverageHandleTimeSeconds),
            ReportFormat.Duration(totals.AverageSpeedOfAnswerSeconds),
            ReportFormat.Number(totals.CurrentWaiting),
        };

        if (includeSlaColumn)
        {
            cells.Add("—");
        }

        return new ReportRow(cells, ReportRowKind.GrandTotal);
    }
}
