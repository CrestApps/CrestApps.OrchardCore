using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The trend of a number answer, such as a rating, Net Promoter Score or satisfaction question: its average and count
/// by local day, week or month.
/// </summary>
internal sealed class SurveyTrendReport : IReport
{
    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;

    internal readonly IStringLocalizer S;

    public SurveyTrendReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        IStringLocalizer<SurveyTrendReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Trend;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyTrendReport>("Satisfaction Trend");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyTrendReport>("The average answer to a rating, score or number question by day, week or month.");

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

        var questionId = context.Filter.GetOrDefault<string>(SurveyReportKeys.TrendQuestionId);
        var question = SurveyDefinitionQuestions.ById(scope.Definition).GetValueOrDefault(questionId ?? string.Empty);

        if (question is null)
        {
            return new ReportDocument().Add(new ReportSection
            {
                Title = scope.Survey.Title,
                Kind = ReportSectionKind.Metrics,
                EmptyMessage = S["Choose a rating, score or number question."].Value,
            });
        }

        var period = context.Filter.GetOrDefault<string>(SurveyReportKeys.TrendPeriod) ?? "week";
        var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var buckets = new SortedDictionary<DateTime, List<decimal>>();

        foreach (var (completedUtc, value) in await _queries.ListNumberValuesAsync(scope.AnswerFilter, question.Id, cancellationToken))
        {
            var local = (await _data.ToLocalAsync(completedUtc)).Date;
            var key = period switch
            {
                "day" => local,
                "month" => new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
                _ => local.AddDays(-(((int)local.DayOfWeek - (int)firstDay + 7) % 7)),
            };

            if (!buckets.TryGetValue(key, out var values))
            {
                buckets[key] = values = [];
            }

            values.Add(value);
        }

        var rows = new List<ReportRow>();
        var hidden = 0;

        foreach (var (start, values) in buckets)
        {
            if (scope.IsTooSmall(values.Count))
            {
                hidden++;

                continue;
            }

            rows.Add(new ReportRow(
            [
                period == "month" ? start.ToString("Y", CultureInfo.CurrentCulture) : start.ToString("d", CultureInfo.CurrentCulture),
                ReportFormat.Number(values.Count),
                SurveyReportData.Number(values.Average()),
            ]));
        }

        var table = ReportSection.ForTable(question.Title ?? question.Name,
            [
                new ReportColumn(period switch
                {
                    "day" => S["Day"].Value,
                    "month" => S["Month"].Value,
                    _ => S["Week of"].Value,
                }),
                new ReportColumn(S["Answers"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average"].Value, ReportColumnAlign.End),
            ],
            rows);
        table.EmptyMessage = S["No answer in these days."].Value;

        if (hidden > 0)
        {
            table.Description = S["{0} periods are hidden because they have fewer than {1} answers.", hidden, scope.MinimumGroupSize].Value;
        }

        return new ReportDocument().Add(table);
    }
}
