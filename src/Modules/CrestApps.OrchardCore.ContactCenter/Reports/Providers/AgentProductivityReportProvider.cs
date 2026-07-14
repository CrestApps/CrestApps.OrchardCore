using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center agent productivity report: per-agent handled volume, talk time, and completed work.
/// </summary>
public sealed class AgentProductivityReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProductivityReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentProductivityReportProvider(
        IContactCenterReportingService reportingService,
        IStringLocalizer<AgentProductivityReportProvider> stringLocalizer)
        : base(reportingService, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-agent-productivity";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Agent productivity"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per-agent handled volume, talk time, average handle time, and completed activities."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.AgentPerformance;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.QueueGroupId,
        ContactCenterReportFilter.QueueId,
        ContactCenterReportFilter.AgentId,
        ContactCenterReportFilter.Channel,
        ContactCenterReportFilter.Direction,
    ];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetAgentProductivityAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Handled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Inbound"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Outbound"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total talk time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg wrap-up time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total wrap-up time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Activities completed"].Value, ReportColumnAlign.End),
        };

        var rows = report.Rows
            .Select(row => new ReportRow(
            [
                ReportValue.UserDisplayName(row.UserName, S["(Unknown agent)"].Value),
                ReportFormat.Number(row.InteractionsHandled),
                ReportFormat.Number(row.InboundHandled),
                ReportFormat.Number(row.OutboundHandled),
                ReportFormat.Duration(row.AverageHandleTimeSeconds),
                ReportFormat.Duration(row.TotalTalkTimeSeconds),
                ReportFormat.Duration(row.AverageWrapUpTimeSeconds),
                ReportFormat.Duration(row.TotalWrapUpTimeSeconds),
                ReportFormat.Number(row.ActivitiesCompleted),
            ]))
            .ToList();

        rows.Add(CreateGrandTotalRow(report.Rows, S["All agents"].Value));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agents"].Value, columns, rows));
    }

    internal static ReportRow CreateGrandTotalRow(
        IEnumerable<AgentProductivityRow> rows,
        string label)
    {
        var handled = rows.Sum(row => row.InteractionsHandled);
        var inbound = rows.Sum(row => row.InboundHandled);
        var outbound = rows.Sum(row => row.OutboundHandled);
        var talkSeconds = rows.Sum(row => row.TotalTalkTimeSeconds);
        var wrapUpSeconds = rows.Sum(row => row.TotalWrapUpTimeSeconds);
        var activitiesCompleted = rows.Sum(row => row.ActivitiesCompleted);

        return new ReportRow(
        [
            label,
            ReportFormat.Number(handled),
            ReportFormat.Number(inbound),
            ReportFormat.Number(outbound),
            ReportFormat.Duration(handled > 0 ? (talkSeconds + wrapUpSeconds) / handled : 0d),
            ReportFormat.Duration(talkSeconds),
            ReportFormat.Duration(handled > 0 ? wrapUpSeconds / handled : 0d),
            ReportFormat.Duration(wrapUpSeconds),
            ReportFormat.Number(activitiesCompleted),
        ], ReportRowKind.GrandTotal);
    }
}
