using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// The Contact Center campaign summary report: per-campaign completed-versus-pending activity progress.
/// </summary>
public sealed class CampaignSummaryReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignSummaryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CampaignSummaryReportProvider(
        IContactCenterReportingService reportingService,
        IStringLocalizer<CampaignSummaryReportProvider> stringLocalizer)
        : base(reportingService, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-campaign-summary";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Campaign summary"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per-campaign completed-versus-pending progress across the activity inventory."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetCampaignSummaryAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var noCampaign = S["(No campaign)"].Value;

        var document = new ReportDocument();

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Total"].Value, ReportFormat.Number(report.Totals.Total)),
            new ReportMetric(S["Completed"].Value, ReportFormat.Number(report.Totals.Completed), ReportFormat.Percent(report.Totals.CompletionRate)),
            new ReportMetric(S["Pending"].Value, ReportFormat.Number(report.Totals.Pending)),
            new ReportMetric(S["In progress"].Value, ReportFormat.Number(report.Totals.InProgress)),
        ]));

        var rows = new List<ReportRow>();

        foreach (var row in report.Rows)
        {
            rows.Add(new ReportRow(ContactCenterReportCells.Progress(
                string.IsNullOrEmpty(row.CampaignName) ? noCampaign : row.CampaignName,
                row.Counts)));
        }

        rows.Add(new ReportRow(ContactCenterReportCells.Progress(S["All campaigns"].Value, report.Totals), emphasize: true));

        document.Add(ReportSection.ForTable(S["Campaigns"].Value, ContactCenterReportCells.ProgressColumns(S, S["Campaign"].Value), rows));

        return document;
    }
}
