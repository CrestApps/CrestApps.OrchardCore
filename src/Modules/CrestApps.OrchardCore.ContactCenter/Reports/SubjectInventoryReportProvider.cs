using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

/// <summary>
/// The Contact Center subject inventory report: per-subject completed-versus-pending activity progress.
/// </summary>
public sealed class SubjectInventoryReportProvider : ContactCenterReportBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectInventoryReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubjectInventoryReportProvider(
        IContactCenterReportingService reportingService,
        IStringLocalizer<SubjectInventoryReportProvider> stringLocalizer)
        : base(reportingService, stringLocalizer)
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
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
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
            rows.Add(new ReportRow(ContactCenterReportCells.Progress(
                string.IsNullOrEmpty(row.SubjectContentType) ? noSubject : row.SubjectContentType,
                row.Counts)));
        }

        rows.Add(new ReportRow(ContactCenterReportCells.Progress(S["All subjects"].Value, report.Totals), emphasize: true));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Subjects"].Value, ContactCenterReportCells.ProgressColumns(S, S["Subject"].Value), rows));
    }
}
