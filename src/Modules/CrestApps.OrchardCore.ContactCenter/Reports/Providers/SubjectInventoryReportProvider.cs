using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center subject inventory report: per-subject completed-versus-pending activity progress.
/// </summary>
public sealed class SubjectInventoryReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectInventoryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectInventoryReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<SubjectInventoryReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => "contact-center-subject-inventory";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Subject inventory"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per-subject completed-versus-pending progress across the activity inventory."];

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
    /// <remarks>Subjects and their activities are CRM data written by the Omnichannel activity feature the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var report = await ReportingService.GetSubjectInventoryAsync(
            context.FromUtc,
            context.ToUtc,
            ContactCenterReportFilter.GetCriteria(context.Filter),
            cancellationToken);

        var noSubject = S["(No subject)"].Value;

        var rows = new List<ReportRow>();

        foreach (var row in report.Rows)
        {
            rows.Add(ContactCenterReportCells.ProgressRow(
                string.IsNullOrEmpty(row.SubjectContentType) ? noSubject : row.SubjectContentType,
                row.Counts));
        }

        rows.Add(ContactCenterReportCells.ProgressRow(
            S["All subjects"].Value,
            report.Totals,
            ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Subjects"].Value, ContactCenterReportCells.ProgressColumns(S, S["Subject"].Value), rows));
    }
}
