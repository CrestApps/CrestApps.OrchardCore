using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments.Reports;

/// <summary>
/// Results of quizzes and the analysis of each question: how hard it is and how well it tells strong learners from weak
/// ones, from the graded attempts started in the date range.
/// </summary>
internal sealed class QuizItemAnalysisReport : IReport
{
    private const int MaxAttempts = 5000;

    private readonly AssessmentAttemptStore _attemptStore;

    internal readonly IStringLocalizer S;

    public QuizItemAnalysisReport(AssessmentAttemptStore attemptStore, IStringLocalizer<QuizItemAnalysisReport> stringLocalizer)
    {
        _attemptStore = attemptStore;
        S = stringLocalizer;
    }

    public static string Name => "learning-quiz-item-analysis";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<QuizItemAnalysisReport>("Quiz results and item analysis");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<QuizItemAnalysisReport>("Pass rates of quizzes, and for each question how hard it is and how well it tells strong learners from weak ones.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => LearningPermissions.ViewLearningReports;

    public static Permission ExportPermission => LearningPermissions.ViewLearningReports;

    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.LastDays(90);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var attempts = await _attemptStore.PageForExportAsync(null, range.FromUtc, range.ToUtc, 0, MaxAttempts, cancellationToken);
        var graded = attempts.Where(attempt => attempt.Status == AttemptStatus.Graded).ToList();
        var analysis = ItemAnalysisCalculator.Analyze(graded);
        var titles = attempts.GroupBy(attempt => attempt.QuizId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last().QuizTitle ?? group.Key, StringComparer.Ordinal);

        var quizzes = graded
            .GroupBy(attempt => attempt.QuizId, StringComparer.Ordinal)
            .Select(group => new ReportRow(
            [
                titles[group.Key],
                ReportFormat.Number(group.Count()),
                ReportFormat.Percent(group.Count(attempt => attempt.Passed == true) / (double)group.Count()),
                ReportFormat.Percent((double)group.Average(attempt => attempt.Percent ?? 0) / 100d),
            ]))
            .ToList();

        var questions = analysis
            .OrderBy(item => titles.GetValueOrDefault(item.QuizId, item.QuizId), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Discrimination ?? 1)
            .Select(item => new ReportRow(
            [
                titles.GetValueOrDefault(item.QuizId, item.QuizId),
                item.Title ?? item.QuestionId,
                item.QuestionType,
                ReportFormat.Number(item.Answers),
                ReportFormat.Percent(item.Difficulty),
                item.Discrimination is { } discrimination ? discrimination.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) : S["Too few attempts"].Value,
            ]))
            .ToList();

        var weak = analysis.Count(item => item.Discrimination is < 0.2);

        var quizTable = ReportSection.ForTable(S["Quizzes"].Value,
        [
            new ReportColumn(S["Quiz"].Value),
            new ReportColumn(S["Graded attempts"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Pass rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Mean score"].Value, ReportColumnAlign.End),
        ], quizzes);

        var questionTable = ReportSection.ForTable(S["Questions"].Value,
        [
            new ReportColumn(S["Quiz"].Value),
            new ReportColumn(S["Question"].Value),
            new ReportColumn(S["Type"].Value),
            new ReportColumn(S["Answers"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Difficulty (share of points)"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Discrimination"].Value, ReportColumnAlign.End),
        ], questions);

        questionTable.Description = S["Difficulty is the share of the points learners got: higher is easier. Discrimination compares the top 27% of attempts with the bottom 27%: below 0.20 the question tells strong and weak learners apart poorly; below 0 it may have a wrong key."].Value;

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Graded attempts"].Value, ReportFormat.Number(graded.Count)),
                new ReportMetric(S["Questions analysed"].Value, ReportFormat.Number(analysis.Count)),
                new ReportMetric(S["Questions to review"].Value, ReportFormat.Number(weak), S["Discrimination below 0.20."].Value),
            ]))
            .Add(quizTable)
            .Add(questionTable);
    }
}
