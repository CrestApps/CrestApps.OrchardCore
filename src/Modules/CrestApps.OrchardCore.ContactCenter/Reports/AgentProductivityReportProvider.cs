using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

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

        var rows = report.Rows.Select(row => new ReportRow(
        [
            row.DisplayName,
            ReportFormat.Number(row.InteractionsHandled),
            ReportFormat.Number(row.InboundHandled),
            ReportFormat.Number(row.OutboundHandled),
            ReportFormat.Duration(row.AverageHandleTimeSeconds),
            ReportFormat.Duration(row.TotalTalkTimeSeconds),
            ReportFormat.Duration(row.AverageWrapUpTimeSeconds),
            ReportFormat.Duration(row.TotalWrapUpTimeSeconds),
            ReportFormat.Number(row.ActivitiesCompleted),
        ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agents"].Value, columns, rows));
    }
}
