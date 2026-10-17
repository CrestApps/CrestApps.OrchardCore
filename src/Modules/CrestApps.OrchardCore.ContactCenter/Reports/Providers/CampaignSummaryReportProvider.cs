using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center campaign summary report: per-campaign completed-versus-pending activity progress.
/// </summary>
public sealed class CampaignSummaryReportProvider : ContactCenterReportBase, IReport
{
    private readonly IDialerDispatchDiagnosticsService _dispatchDiagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignSummaryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="dispatchDiagnostics">The service that explains why pending records are not being dialed.</param>
    public CampaignSummaryReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<CampaignSummaryReportProvider> stringLocalizer,
        IDialerDispatchDiagnosticsService dispatchDiagnostics)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _dispatchDiagnostics = dispatchDiagnostics;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-campaign-summary";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CampaignSummaryReportProvider>("Campaign summary");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<CampaignSummaryReportProvider>("Per-campaign completed-versus-pending progress across the activity inventory.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.CrmCampaigns;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

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
    /// <remarks>Campaigns and their activities are CRM data written by the Omnichannel activity feature the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var report = await ReportingService.GetCampaignSummaryAsync(
            range.FromUtc.GetValueOrDefault(),
            range.ToUtc.GetValueOrDefault(),
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
            new ReportMetric(S["Not in service"].Value, ReportFormat.Number(report.Totals.NotInService)),
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

        await AddDispatchSectionsAsync(document, report, cancellationToken);

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

    // The report counts what is pending over the period it covers. Why those records are not being dialed is true of right
    // now, whatever the period, so it is read live and kept apart from them.
    private async Task AddDispatchSectionsAsync(ReportDocument document, CampaignSummaryReport report, CancellationToken cancellationToken)
    {
        var campaigns = report.Rows
            .Where(row => !string.IsNullOrEmpty(row.CampaignId) && row.Counts.Pending > 0)
            .ToArray();

        if (campaigns.Length == 0)
        {
            return;
        }

        var diagnoses = await _dispatchDiagnostics.DiagnoseAsync(
            campaigns.Select(row => ContactCenterConstants.CampaignQueue.CreateId(row.CampaignId)).ToArray(),
            cancellationToken);

        var summaryRows = new List<ReportRow>();
        var reasonRows = new List<ReportRow>();

        foreach (var row in campaigns)
        {
            var diagnosis = diagnoses.FirstOrDefault(candidate => string.Equals(candidate.CampaignId, row.CampaignId, StringComparison.Ordinal));

            if (diagnosis is null || diagnosis.PendingCount == 0)
            {
                continue;
            }

            var name = string.IsNullOrEmpty(row.CampaignName) ? S["(Unknown campaign)"].Value : row.CampaignName;

            summaryRows.Add(new ReportRow(
            [
                name,
                ReportFormat.Number(diagnosis.PendingCount),
                ReportFormat.Number(diagnosis.DialableNowCount),
                ReportFormat.Number(diagnosis.HeldBackCount),
            ]));

            foreach (var bucket in diagnosis.Buckets)
            {
                reasonRows.Add(new ReportRow(
                [
                    name,
                    DispatchBlockerLabels.Describe(S, bucket.Blocker),
                    ReportFormat.Number(bucket.Count),
                ]));
            }

            if (diagnosis.EvaluatedCount < diagnosis.PendingCount)
            {
                reasonRows.Add(new ReportRow(
                [
                    name,
                    S["Not explained (more records than one pass reads)"].Value,
                    ReportFormat.Number(diagnosis.PendingCount - diagnosis.EvaluatedCount),
                ]));
            }
        }

        if (summaryRows.Count == 0)
        {
            return;
        }

        document.Add(ReportSection.ForTable(
            S["Pending right now: dialable versus held back"].Value,
            [
                new ReportColumn(S["Campaign"].Value),
                new ReportColumn(S["Pending now"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Dialable now"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Held back"].Value, ReportColumnAlign.End),
            ],
            summaryRows));

        document.Add(ReportSection.ForTable(
            S["Why pending records are not dialing"].Value,
            [
                new ReportColumn(S["Campaign"].Value),
                new ReportColumn(S["Reason"].Value),
                new ReportColumn(S["Records"].Value, ReportColumnAlign.End),
            ],
            reasonRows));
    }
}
