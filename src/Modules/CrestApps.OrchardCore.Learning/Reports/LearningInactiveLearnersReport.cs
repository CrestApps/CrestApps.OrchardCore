using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// The people taking a course who have not opened it for longer than the inactive period of the Learning upkeep
/// settings, longest idle first. The report reads the current state, so it has no period.
/// </summary>
internal sealed class LearningInactiveLearnersReport : IReport
{
    private readonly EnrollmentStore _enrollmentStore;
    private readonly LearningReportText _text;
    private readonly IClock _clock;
    private readonly LearningUpkeepOptions _options;

    internal readonly IStringLocalizer S;

    public LearningInactiveLearnersReport(
        EnrollmentStore enrollmentStore,
        LearningReportText text,
        IClock clock,
        IOptions<LearningUpkeepOptions> options,
        IStringLocalizer<LearningInactiveLearnersReport> stringLocalizer)
    {
        _enrollmentStore = enrollmentStore;
        _text = text;
        _clock = clock;
        _options = options.Value;
        S = stringLocalizer;
    }

    public static string Name => "learning-inactive-learners";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningInactiveLearnersReport>("Inactive learners");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningInactiveLearnersReport>("People taking a course who have not opened it for longer than the inactive period.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => LearningPermissions.ViewLearningReports;

    public static Permission ExportPermission => LearningPermissions.ExportLearnerRecords;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.AllTime;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        if (_options.InactiveDays <= 0)
        {
            return new ReportDocument().Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Inactive learners"].Value, ReportFormat.Number(0), S["The inactive period is turned off in the Learning upkeep settings."].Value),
            ]));
        }

        var now = _clock.UtcNow;
        var enrollments = (await _enrollmentStore.ListInactiveAsync(now.AddDays(-_options.InactiveDays), LearningReportText.MaxRows, cancellationToken))
            .OrderBy(enrollment => enrollment.LastActivityUtc ?? enrollment.CreatedUtc)
            .ToArray();

        await _text.LoadAsync(enrollments);

        var rows = new List<ReportRow>();

        foreach (var enrollment in enrollments)
        {
            var idleSince = enrollment.LastActivityUtc ?? enrollment.CreatedUtc;

            rows.Add(new ReportRow(
            [
                _text.Learner(enrollment),
                _text.Course(enrollment),
                await _text.TimeAsync(enrollment.CreatedUtc),
                enrollment.LastActivityUtc is null ? S["Never"].Value : await _text.TimeAsync(enrollment.LastActivityUtc),
                ReportFormat.Number((long)(now - idleSince).TotalDays),
                ReportFormat.Percent(enrollment.ProgressPercent / 100d),
                await _text.TimeAsync(enrollment.DueUtc),
            ]));
        }

        var table = ReportSection.ForTable(S["Inactive learners"].Value,
        [
            new ReportColumn(S["Learner"].Value),
            new ReportColumn(S["Course"].Value),
            new ReportColumn(S["Enrolled"].Value),
            new ReportColumn(S["Last activity"].Value),
            new ReportColumn(S["Days idle"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Progress"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Due"].Value),
        ], rows);

        table.Description = S["Active enrollments with no activity for more than {0} days.", _options.InactiveDays].Value;

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Inactive learners"].Value, ReportFormat.Number(enrollments.Select(enrollment => enrollment.UserId).Distinct(StringComparer.Ordinal).Count())),
                new ReportMetric(S["Enrollments"].Value, ReportFormat.Number(enrollments.Length)),
            ]))
            .Add(table);
    }
}
