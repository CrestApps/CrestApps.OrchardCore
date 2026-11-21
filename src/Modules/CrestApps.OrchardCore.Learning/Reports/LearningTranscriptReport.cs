using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// The courses people completed in the period, with the grade and the cycle, for one learner or for everyone.
/// </summary>
internal sealed class LearningTranscriptReport : IReport
{
    /// <summary>
    /// The filter key that holds the user name of the learner.
    /// </summary>
    public const string LearnerFilterKey = "LearningLearner";

    private readonly EnrollmentStore _enrollmentStore;
    private readonly LearnerUserLookup _userLookup;
    private readonly LearningReportText _text;

    internal readonly IStringLocalizer S;

    public LearningTranscriptReport(
        EnrollmentStore enrollmentStore,
        LearnerUserLookup userLookup,
        LearningReportText text,
        IStringLocalizer<LearningTranscriptReport> stringLocalizer)
    {
        _enrollmentStore = enrollmentStore;
        _userLookup = userLookup;
        _text = text;
        S = stringLocalizer;
    }

    public static string Name => "learning-transcript";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningTranscriptReport>("Learner transcript");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningTranscriptReport>("The courses completed in the period, with grade and cycle, for one learner or for everyone.");

    public static string Category => ReportsConstants.Categories.ComplianceAudit;

    public static Permission Permission => LearningPermissions.ViewLearningReports;

    public static Permission ExportPermission => LearningPermissions.ExportLearnerRecords;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var learner = context.Filter.GetOrDefault<string>(LearnerFilterKey);
        string userId = null;

        if (!string.IsNullOrWhiteSpace(learner))
        {
            userId = await _userLookup.FindUserIdAsync(learner);

            if (userId is null)
            {
                return new ReportDocument().Add(ReportSection.ForMetrics(S["Summary"].Value,
                [
                    new ReportMetric(S["Completions"].Value, ReportFormat.Number(0), S["No user is named {0}.", learner].Value),
                ]));
            }
        }

        var enrollments = await _enrollmentStore.ListCompletedAsync(range.FromUtc, range.ToUtc, userId, LearningReportText.MaxRows, cancellationToken);

        await _text.LoadAsync(enrollments);

        var rows = new List<ReportRow>();

        foreach (var enrollment in enrollments)
        {
            rows.Add(new ReportRow(
            [
                _text.Learner(enrollment),
                _text.Course(enrollment),
                enrollment.Number,
                await _text.TimeAsync(enrollment.CompletedUtc),
                LearningReportText.Grade(enrollment.Grade),
                enrollment.Passed switch { true => S["Yes"].Value, false => S["No"].Value, _ => string.Empty },
                ReportFormat.Number(enrollment.Cycle),
            ]));
        }

        var table = ReportSection.ForTable(S["Completions"].Value,
        [
            new ReportColumn(S["Learner"].Value),
            new ReportColumn(S["Course"].Value),
            new ReportColumn(S["Enrollment"].Value),
            new ReportColumn(S["Completed"].Value),
            new ReportColumn(S["Grade"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Passed"].Value),
            new ReportColumn(S["Cycle"].Value, ReportColumnAlign.End),
        ], rows);

        if (enrollments.Count >= LearningReportText.MaxRows)
        {
            table.Description = S["Only the first {0} completions of the period are listed. Choose a shorter period or one learner.", LearningReportText.MaxRows].Value;
        }

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Completions"].Value, ReportFormat.Number(enrollments.Count)),
                new ReportMetric(S["Learners"].Value, ReportFormat.Number(enrollments.Select(enrollment => enrollment.UserId).Distinct(StringComparer.Ordinal).Count())),
            ]))
            .Add(table);
    }
}
