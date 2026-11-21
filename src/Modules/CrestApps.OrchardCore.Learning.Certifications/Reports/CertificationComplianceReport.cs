using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Certifications.Reports;

/// <summary>
/// Where people stand on each certification path today: certified, renewal open, expiring within 30 days, lapsed,
/// revoked and still working on a first certificate, with the people whose certificate expires soonest.
/// </summary>
internal sealed class CertificationComplianceReport : IReport
{
    private const int MaxPeople = 5000;

    private readonly ICertificationPathService _paths;
    private readonly CertificationPathProgressStore _progress;
    private readonly ICertificationManager _certifications;
    private readonly LearnerUserLookup _users;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public CertificationComplianceReport(
        ICertificationPathService paths,
        CertificationPathProgressStore progress,
        ICertificationManager certifications,
        LearnerUserLookup users,
        ILocalClock localClock,
        IStringLocalizer<CertificationComplianceReport> stringLocalizer)
    {
        _paths = paths;
        _progress = progress;
        _certifications = certifications;
        _users = users;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public static string Name => "learning-certification-compliance";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<CertificationComplianceReport>("Certification compliance");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<CertificationComplianceReport>("Who holds each certification earned through learning, whose renewal is open, whose certificate expires within 30 days, and who lapsed.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => LearningPermissions.ViewLearningReports;

    public static Permission ExportPermission => LearningPermissions.ViewLearningReports;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var soon = today.AddDays(30);
        var paths = new List<ReportRow>();
        var expiring = new List<(DateOnly ExpiresOn, string UserId, string Certification, CertificationPathState State)>();
        var totals = new Dictionary<CertificationPathState, int>();

        foreach (var path in await _paths.ListAsync(cancellationToken))
        {
            var certification = await _certifications.FindByIdAsync(path.CertificationId, cancellationToken);
            var (_, people) = await _progress.PageAsync(path.ItemId, null, 0, MaxPeople, cancellationToken);
            int Count(CertificationPathState state) => people.Count(item => item.State == state);
            var expiringSoon = people.Count(item => item.State is CertificationPathState.Certified or CertificationPathState.RenewalOpen && item.ExpiresOn is { } expiresOn && expiresOn <= soon);

            foreach (var state in Enum.GetValues<CertificationPathState>())
            {
                totals[state] = totals.GetValueOrDefault(state) + Count(state);
            }

            expiring.AddRange(people
                .Where(item => item.State is CertificationPathState.Certified or CertificationPathState.RenewalOpen && item.ExpiresOn is { } expiresOn && expiresOn <= soon)
                .Select(item => (item.ExpiresOn.Value, item.UserId, certification?.Name ?? path.CertificationId, item.State)));

            paths.Add(new ReportRow(
            [
                certification?.Name ?? path.CertificationId,
                ReportFormat.Number(Count(CertificationPathState.Certified) + Count(CertificationPathState.RenewalOpen)),
                ReportFormat.Number(Count(CertificationPathState.RenewalOpen)),
                ReportFormat.Number(expiringSoon),
                ReportFormat.Number(Count(CertificationPathState.Lapsed)),
                ReportFormat.Number(Count(CertificationPathState.Revoked)),
                ReportFormat.Number(Count(CertificationPathState.InProgress)),
            ]));
        }

        var names = await _users.GetUserNamesAsync(expiring.Select(item => item.UserId));
        var expiringRows = expiring
            .OrderBy(item => item.ExpiresOn)
            .Select(item => new ReportRow(
            [
                names.TryGetValue(item.UserId, out var name) ? name : item.UserId,
                item.Certification,
                item.ExpiresOn.ToString("d", System.Globalization.CultureInfo.CurrentCulture),
                item.State == CertificationPathState.RenewalOpen ? S["Renewal open"].Value : S["Certified"].Value,
            ]))
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Today"].Value,
            [
                new ReportMetric(S["Holding a valid certificate"].Value, ReportFormat.Number(totals.GetValueOrDefault(CertificationPathState.Certified) + totals.GetValueOrDefault(CertificationPathState.RenewalOpen))),
                new ReportMetric(S["Expiring within 30 days"].Value, ReportFormat.Number(expiringRows.Count)),
                new ReportMetric(S["Lapsed"].Value, ReportFormat.Number(totals.GetValueOrDefault(CertificationPathState.Lapsed))),
                new ReportMetric(S["Working on a first certificate"].Value, ReportFormat.Number(totals.GetValueOrDefault(CertificationPathState.InProgress))),
            ]))
            .Add(ReportSection.ForTable(S["Certifications"].Value,
            [
                new ReportColumn(S["Certification"].Value),
                new ReportColumn(S["Valid"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Renewal open"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Expiring within 30 days"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Lapsed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Revoked"].Value, ReportColumnAlign.End),
                new ReportColumn(S["In progress"].Value, ReportColumnAlign.End),
            ], paths))
            .Add(ReportSection.ForTable(S["Expiring within 30 days"].Value,
            [
                new ReportColumn(S["Person"].Value),
                new ReportColumn(S["Certification"].Value),
                new ReportColumn(S["Valid until"].Value),
                new ReportColumn(S["State"].Value),
            ], expiringRows));
    }
}
