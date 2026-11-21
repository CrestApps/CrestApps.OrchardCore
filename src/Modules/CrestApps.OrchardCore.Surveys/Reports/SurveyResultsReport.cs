using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The results of a survey: how many responded and finished, how long it took, then one section per question from its
/// question type's analyzer, optionally compared across the choices of one question.
/// </summary>
internal sealed class SurveyResultsReport : IReport
{
    private const int MaxSegments = 6;

    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly ISurveyAnswerAggregates _aggregates;
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    public SurveyResultsReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        ISurveyAnswerAggregates aggregates,
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyResultsReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _aggregates = aggregates;
        _registry = registry;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Results;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyResultsReport>("Survey Results");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyResultsReport>("How many people responded and finished a survey, how long it took, and the answers to each question, optionally compared across the choices of one question.");

    public static string Category => SurveyReportNames.Category;

    public static Permission Permission => SurveysPermissions.ViewOwnSurveyResponses;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _data.ResolveAsync(context, cancellationToken);

        if (scope is null)
        {
            return _data.ChooseSurvey();
        }

        var completed = await _aggregates.CountResponsesAsync(scope.AnswerFilter, cancellationToken);

        if (scope.IsTooSmall(completed))
        {
            return _data.TooFewResponses(scope);
        }

        var rows = await _queries.ListRowsAsync(scope.ResultsScope, cancellationToken);
        var finished = rows.Where(row => row.CompletedUtc is not null && row.Status == nameof(SurveyResponseStatus.Completed)).ToList();
        var durations = finished.Select(row => (decimal)(row.CompletedUtc.Value - row.StartedUtc).TotalSeconds).Where(seconds => seconds >= 0).Order().ToList();
        var median = SurveyNumberSummary.Median(durations);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(scope.Survey.Title,
            [
                new ReportMetric(S["Completed responses"].Value, ReportFormat.Number(completed)),
                new ReportMetric(S["Started"].Value, ReportFormat.Number(rows.Count), S["Responses started in these days, finished or not."].Value),
                new ReportMetric(S["Completion rate"].Value, rows.Count == 0 ? "-" : ReportFormat.Percent((double)finished.Count / rows.Count)),
                new ReportMetric(S["Median time"].Value, median is { } seconds ? ReportFormat.Duration((double)seconds) : "-"),
            ]));

        var analysis = new SurveyAnalysisContext
        {
            Survey = scope.Survey,
            Filter = scope.AnswerFilter,
            Aggregates = _aggregates,
            Culture = CultureInfo.CurrentUICulture,
            Format = new SurveyFormatContext { Culture = CultureInfo.CurrentCulture, Definition = scope.Definition },
            MinimumGroupSize = scope.IsAnonymous ? scope.MinimumGroupSize : 0,
        };

        foreach (var (_, question) in SurveyDefinitionQuestions.All(scope.Definition))
        {
            if (_registry.GetAnalyzer(question.ControlType) is not { } analyzer)
            {
                continue;
            }

            var summary = await analyzer.SummarizeAsync(question, analysis, cancellationToken);

            foreach (var section in analyzer.ToReportSections(question, summary, analysis) ?? [])
            {
                document.Add(section);
            }
        }

        await AddSegmentsAsync(document, scope, context, cancellationToken);

        return document;
    }

    private async Task AddSegmentsAsync(ReportDocument document, SurveyReportScope scope, ReportContext context, CancellationToken cancellationToken)
    {
        var segmentId = context.Filter.GetOrDefault<string>(SurveyReportKeys.SegmentQuestionId);
        var questions = SurveyDefinitionQuestions.ById(scope.Definition);

        if (string.IsNullOrEmpty(segmentId) || !questions.TryGetValue(segmentId, out var segmentQuestion))
        {
            return;
        }

        var segments = new List<(string Label, IReadOnlyCollection<string> ResponseIds)>();
        var hidden = 0;

        foreach (var choice in segmentQuestion.Choices.Where(choice => !choice.Retired).Take(MaxSegments))
        {
            var ids = await _aggregates.ResponsesWithChoiceAsync(scope.AnswerFilter, segmentQuestion.Id, choice.Id, cancellationToken);

            if (scope.IsTooSmall(ids.Count))
            {
                hidden++;

                continue;
            }

            segments.Add((choice.Text ?? choice.Value, ids));
        }

        if (segments.Count == 0)
        {
            return;
        }

        foreach (var (_, question) in SurveyDefinitionQuestions.All(scope.Definition))
        {
            var descriptor = _registry.GetControl(question.ControlType);

            if (question.Id == segmentQuestion.Id || descriptor?.Has(SurveyControlCapabilities.UsesChoices) != true || question.Rows.Count > 0)
            {
                continue;
            }

            var columns = new List<ReportColumn> { new(S["Answer"].Value) };
            var counts = new List<IReadOnlyDictionary<string, long>>();
            var totals = new List<long>();

            foreach (var (label, ids) in segments)
            {
                var filter = Copy(scope.AnswerFilter, ids);
                var choices = await _aggregates.CountChoicesAsync(filter, question.Id, cancellationToken);

                columns.Add(new ReportColumn(label, ReportColumnAlign.End));
                counts.Add(choices.GetValueOrDefault(string.Empty) ?? new Dictionary<string, long>());
                totals.Add(ids.Count);
            }

            var rows = question.Choices
                .Where(choice => !choice.Retired)
                .Select(choice => new ReportRow(
                [
                    choice.Text ?? choice.Value,
                    .. counts.Select((choices, index) => Cell(choices.GetValueOrDefault(choice.Id), totals[index])),
                ]))
                .ToList();

            var table = ReportSection.ForTable(S["{0} by {1}", question.Title ?? question.Name, segmentQuestion.Title ?? segmentQuestion.Name].Value, columns, rows);
            table.Description = hidden > 0
                ? S["Each column counts the responses that chose that answer. {0} groups are hidden because they have fewer than {1} responses.", hidden, scope.MinimumGroupSize].Value
                : S["Each column counts the responses that chose that answer."].Value;
            document.Add(table);
        }
    }

    private static string Cell(long count, long total)
        => total <= 0
            ? ReportFormat.Number(count)
            : string.Create(CultureInfo.CurrentCulture, $"{ReportFormat.Number(count)} ({ReportFormat.Percent((double)count / total)})");

    private static SurveyAnswerFilter Copy(SurveyAnswerFilter filter, IReadOnlyCollection<string> responseIds)
        => new()
        {
            SurveyId = filter.SurveyId,
            VersionNumbers = filter.VersionNumbers,
            CompletedFromUtc = filter.CompletedFromUtc,
            CompletedToUtc = filter.CompletedToUtc,
            CollectorId = filter.CollectorId,
            ContextKind = filter.ContextKind,
            ContextRef = filter.ContextRef,
            ResponseIds = responseIds,
        };
}
