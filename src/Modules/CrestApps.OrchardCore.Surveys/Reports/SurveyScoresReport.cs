using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The scores of a quiz: how scores spread, the pass rate, the average of each score category, and each respondent's best
/// and latest score when the survey is identified and the viewer may see who responded. Scores below the pass mark are
/// colored with the shared report colorizer.
/// </summary>
internal sealed class SurveyScoresReport : IReport
{
    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly SurveyRespondentNames _names;

    internal readonly IStringLocalizer S;

    public SurveyScoresReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        SurveyRespondentNames names,
        IStringLocalizer<SurveyScoresReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _names = names;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Scores;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyScoresReport>("Quiz Results");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyScoresReport>("How the scores of a quiz spread, how many passed, the average of each score category, and each person's best and latest score.");

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

        scope.ResultsScope.CompletedOnly = true;
        var rows = (await _queries.ListRowsAsync(scope.ResultsScope, cancellationToken)).Where(row => row.Percent is not null).ToList();

        if (scope.IsTooSmall(rows.Count))
        {
            return _data.TooFewResponses(scope);
        }

        var percents = rows.Select(row => row.Percent.Value).Order().ToList();
        var graded = rows.Where(row => row.Passed is not null).ToList();
        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(scope.Survey.Title,
            [
                new ReportMetric(S["Scored responses"].Value, ReportFormat.Number(rows.Count)),
                new ReportMetric(S["Average"].Value, percents.Count == 0 ? "-" : SurveyReportData.Percent(percents.Average())),
                new ReportMetric(S["Median"].Value, SurveyReportData.Percent(SurveyNumberSummary.Median(percents))),
                new ReportMetric(S["Pass rate"].Value, graded.Count == 0 ? "-" : ReportFormat.Percent((double)graded.Count(row => row.Passed == true) / graded.Count), S["The share of responses that reached the pass mark."].Value),
            ]));

        var buckets = Enumerable.Range(0, 10)
            .Select(bucket =>
            {
                var from = bucket * 10;
                var count = percents.Count(percent => bucket == 9 ? percent >= from : percent >= from && percent < from + 10);

                return new ReportBar(
                    bucket == 9 ? "90–100%" : string.Create(CultureInfo.CurrentCulture, $"{from}–{from + 9}%"),
                    ReportFormat.Number(count),
                    percents.Count == 0 ? 0 : (double)count / percents.Count);
            });

        document.Add(ReportSection.ForBars(S["How scores spread"].Value, buckets));

        await AddCategoriesAsync(document, scope, cancellationToken);

        if (scope.CanSeeIdentity)
        {
            var people = new List<ReportRow>();
            var belowPass = scope.Definition?.Scoring?.PassMarkPercent is { } passMark
                ? ReportColumnColorizer.Create(DataFieldType.Decimal, SurveyReportColors.BelowPass(passMark), null, await _data.NowAsync())
                : null;

            foreach (var person in rows.Where(row => !string.IsNullOrEmpty(row.UserId)).GroupBy(row => row.UserId, StringComparer.Ordinal))
            {
                var latest = person.OrderByDescending(row => row.CompletedUtc).First();
                var best = person.Max(row => row.Percent);
                var row = new ReportRow(
                [
                    await _names.GetUserAsync(person.Key, cancellationToken),
                    ReportFormat.Number(person.Count()),
                    SurveyReportData.Percent(best),
                    SurveyReportData.Percent(latest.Percent),
                    await _data.LocalAsync(latest.CompletedUtc),
                ]);

                if (belowPass?.StyleOf(best) is { } bestStyle)
                {
                    row.WithCellStyle(2, bestStyle);
                }

                if (belowPass?.StyleOf(latest.Percent) is { } latestStyle)
                {
                    row.WithCellStyle(3, latestStyle);
                }

                people.Add(row);
            }

            var table = ReportSection.ForTable(S["People"].Value,
                [
                    new ReportColumn(S["Respondent"].Value),
                    new ReportColumn(S["Attempts"].Value, ReportColumnAlign.End),
                    new ReportColumn(S["Best"].Value, ReportColumnAlign.End),
                    new ReportColumn(S["Latest"].Value, ReportColumnAlign.End),
                    new ReportColumn(S["Latest attempt"].Value),
                ],
                people.OrderBy(row => row.Cells[0], StringComparer.CurrentCultureIgnoreCase));
            table.EmptyMessage = S["No signed-in respondent has a score in these days."].Value;
            document.Add(table);
        }

        return document;
    }

    private async Task AddCategoriesAsync(ReportDocument document, SurveyReportScope scope, CancellationToken cancellationToken)
    {
        var categories = scope.Definition?.ScoreCategories ?? [];

        if (categories.Count == 0)
        {
            return;
        }

        var totals = new Dictionary<string, (decimal Sum, int Count)>(StringComparer.Ordinal);

        await foreach (var response in _queries.ReadAsync(scope.ResultsScope, cancellationToken))
        {
            foreach (var (category, score) in response.CategoryScores ?? [])
            {
                var current = totals.GetValueOrDefault(category);
                totals[category] = (current.Sum + score, current.Count + 1);
            }
        }

        var rows = categories
            .Select(category =>
            {
                var total = totals.GetValueOrDefault(category.Id);

                return new ReportRow(
                [
                    category.Title ?? category.Name,
                    ReportFormat.Number(total.Count),
                    total.Count == 0 ? "-" : SurveyReportData.Number(total.Sum / total.Count),
                ]);
            });

        document.Add(ReportSection.ForTable(S["Score categories"].Value,
            [
                new ReportColumn(S["Category"].Value),
                new ReportColumn(S["Responses"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average points"].Value, ReportColumnAlign.End),
            ],
            rows));
    }
}
