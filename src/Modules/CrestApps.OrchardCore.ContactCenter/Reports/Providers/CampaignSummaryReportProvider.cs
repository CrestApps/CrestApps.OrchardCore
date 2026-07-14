using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

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
    public override string Category => ReportsConstants.Categories.CrmCampaigns;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.CampaignGroupId,
        ContactCenterReportFilter.CampaignId,
        ContactCenterReportFilter.Channel,
        ContactCenterReportFilter.ActivitySource,
        ContactCenterReportFilter.ActivityStatus,
    ];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetCampaignSummaryAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var noCampaign = S["(No campaign)"].Value;
        var unknownCampaign = S["(Unknown campaign)"].Value;

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
            rows.Add(ContactCenterReportCells.ProgressRow(
                string.IsNullOrEmpty(row.CampaignId)
                    ? noCampaign
                    : string.IsNullOrEmpty(row.CampaignName)
                        ? unknownCampaign
                        : row.CampaignName,
                row.Counts));
        }

        rows.Add(ContactCenterReportCells.ProgressRow(
            S["All campaigns"].Value,
            report.Totals,
            ReportRowKind.GrandTotal));

        document.Add(ReportSection.ForTable(S["Campaigns"].Value, ContactCenterReportCells.ProgressColumns(S, S["Campaign"].Value), rows));

        var noCampaignGroup = S["(No campaign group)"].Value;
        var unknownCampaignGroup = S["(Unknown campaign group)"].Value;
        var groupRows = new List<ReportRow>();

        foreach (var row in report.GroupRows)
        {
            groupRows.Add(ContactCenterReportCells.ProgressRow(
                string.IsNullOrEmpty(row.CampaignGroupId)
                    ? noCampaignGroup
                    : string.IsNullOrEmpty(row.CampaignGroupName)
                        ? unknownCampaignGroup
                        : row.CampaignGroupName,
                row.Counts,
                ReportRowKind.Subtotal));
        }

        groupRows.Add(ContactCenterReportCells.ProgressRow(
            S["All campaign groups"].Value,
            report.Totals,
            ReportRowKind.GrandTotal));
        document.Add(ReportSection.ForTable(
            S["Campaign groups"].Value,
            ContactCenterReportCells.ProgressColumns(S, S["Campaign group"].Value),
            groupRows));

        return document;
    }
}
