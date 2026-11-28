using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The item analysis of a quiz: for each scored question, how many answered, its difficulty (the mean share of points)
/// and its discrimination (how much better the top 27% of responses did than the bottom 27%).
/// </summary>
internal sealed class SurveyItemAnalysisReport : IReport
{
    private const decimal WeakDiscrimination = 0.2m;

    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    public SurveyItemAnalysisReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyItemAnalysisReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _registry = registry;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.ItemAnalysis;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyItemAnalysisReport>("Item Analysis");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyItemAnalysisReport>("For each scored question of a quiz: how hard it is and how well it tells strong respondents from weak ones.");

    public static string Category => SurveyReportNames.Category;

    public static Permission Permission => SurveysPermissions.ViewOwnSurveyResponses;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(90);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _data.ResolveAsync(context, cancellationToken);

        if (scope is null)
        {
            return _data.ChooseSurvey();
        }

        scope.ResultsScope.CompletedOnly = true;
        var percents = (await _queries.ListRowsAsync(scope.ResultsScope, cancellationToken))
            .Where(row => row.Percent is not null)
            .GroupBy(row => row.ResponseId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Percent.Value, StringComparer.Ordinal);

        if (scope.IsTooSmall(percents.Count))
        {
            return _data.TooFewResponses(scope);
        }

        var scores = await _queries.ListQuestionScoresAsync(scope.AnswerFilter, cancellationToken);
        var analysis = SurveyItemAnalysisCalculator.Analyze(percents, scores).ToDictionary(item => item.QuestionId, StringComparer.Ordinal);
        var rows = new List<ReportRow>();
        var weak = ReportColumnColorizer.Create(DataFieldType.Decimal, SurveyReportColors.WeakBelow(WeakDiscrimination), null, await _data.NowAsync());

        foreach (var (_, question) in SurveyDefinitionQuestions.All(scope.Definition))
        {
            if (!analysis.TryGetValue(question.Id, out var item))
            {
                continue;
            }

            var row = new ReportRow(
            [
                question.Title ?? question.Name,
                _registry.GetControl(question.ControlType)?.Name ?? question.ControlType,
                ReportFormat.Number(item.Answers),
                ReportFormat.Percent(item.Difficulty),
                item.Discrimination is { } discrimination ? discrimination.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) : "-",
            ]);

            rows.Add(item.Discrimination is { } value && weak?.StyleOf(value) is { } style ? row.WithCellStyle(4, style) : row);
        }

        var table = ReportSection.ForTable(S["Questions"].Value,
            [
                new ReportColumn(S["Question"].Value),
                new ReportColumn(S["Type"].Value),
                new ReportColumn(S["Answers"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Difficulty"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Discrimination"].Value, ReportColumnAlign.End),
            ],
            rows);
        table.Description = S["Difficulty is the share of the points respondents got: higher is easier. Discrimination compares the top and the bottom 27% of responses; below 0.2, the question tells them apart poorly."].Value;
        table.EmptyMessage = S["No scored question was answered in these days."].Value;

        return new ReportDocument().Add(table);
    }
}
