using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// How many people enrolled in each course within the period, how many completed it, and the average grade.
/// </summary>
internal sealed class LearningCourseCompletionReport : IReport
{
    private readonly EnrollmentStore _enrollmentStore;
    private readonly LearningReportText _text;

    internal readonly IStringLocalizer S;

    public LearningCourseCompletionReport(
        EnrollmentStore enrollmentStore,
        LearningReportText text,
        IStringLocalizer<LearningCourseCompletionReport> stringLocalizer)
    {
        _enrollmentStore = enrollmentStore;
        _text = text;
        S = stringLocalizer;
    }

    public static string Name => "learning-course-completion";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningCourseCompletionReport>("Course completion");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningCourseCompletionReport>("For each course, the people who enrolled in the period, how many completed it and their average grade.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => LearningPermissions.ViewLearningReports;

    public static Permission ExportPermission => LearningPermissions.ExportLearnerRecords;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var enrollments = await _enrollmentStore.PageForExportAsync(null, range.FromUtc, range.ToUtc, 0, LearningReportText.MaxRows, cancellationToken);

        await _text.LoadAsync(enrollments);

        var courses = enrollments
            .GroupBy(enrollment => enrollment.CourseId, StringComparer.Ordinal)
            .Select(group => new
            {
                Title = _text.Course(group.First()),
                Enrolled = group.Count(),
                Active = group.Count(enrollment => enrollment.Status == EnrollmentStatus.Active),
                Completed = group.Count(enrollment => enrollment.Status == EnrollmentStatus.Completed),
                Ended = group.Count(enrollment => enrollment.Status is EnrollmentStatus.Expired or EnrollmentStatus.Withdrawn),
                Grades = group.Where(enrollment => enrollment.Status == EnrollmentStatus.Completed && enrollment.Grade.HasValue).Select(enrollment => enrollment.Grade.Value).ToArray(),
            })
            .OrderBy(course => course.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var rows = courses
            .Select(course => new ReportRow(
            [
                course.Title,
                ReportFormat.Number(course.Enrolled),
                ReportFormat.Number(course.Active),
                ReportFormat.Number(course.Completed),
                ReportFormat.Number(course.Ended),
                ReportFormat.Percent(course.Enrolled == 0 ? 0 : (double)course.Completed / course.Enrolled),
                course.Grades.Length == 0 ? string.Empty : LearningReportText.Grade(course.Grades.Average()),
            ]))
            .ToList();

        var enrolled = courses.Sum(course => course.Enrolled);
        var completed = courses.Sum(course => course.Completed);

        rows.Add(new ReportRow(
        [
            S["Total"].Value,
            ReportFormat.Number(enrolled),
            ReportFormat.Number(courses.Sum(course => course.Active)),
            ReportFormat.Number(completed),
            ReportFormat.Number(courses.Sum(course => course.Ended)),
            ReportFormat.Percent(enrolled == 0 ? 0 : (double)completed / enrolled),
            string.Empty,
        ], ReportRowKind.GrandTotal));

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Enrolled"].Value, ReportFormat.Number(enrolled)),
                new ReportMetric(S["Completed"].Value, ReportFormat.Number(completed)),
                new ReportMetric(S["Completion rate"].Value, ReportFormat.Percent(enrolled == 0 ? 0 : (double)completed / enrolled)),
                new ReportMetric(S["Courses"].Value, ReportFormat.Number(courses.Length)),
            ]))
            .Add(ReportSection.ForTable(S["Courses"].Value,
            [
                new ReportColumn(S["Course"].Value),
                new ReportColumn(S["Enrolled"].Value, ReportColumnAlign.End),
                new ReportColumn(S["In progress"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Expired or withdrawn"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average grade"].Value, ReportColumnAlign.End),
            ], rows));

        if (enrollments.Count >= LearningReportText.MaxRows)
        {
            document.Sections[^1].Description = S["Only the first {0} enrollments of the period are counted. Choose a shorter period.", LearningReportText.MaxRows].Value;
        }

        return document;
    }
}
