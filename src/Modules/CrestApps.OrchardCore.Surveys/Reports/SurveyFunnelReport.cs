using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The completion funnel of a survey: how many respondents reached each page, how many left the survey there, and the
/// median time spent on it.
/// </summary>
internal sealed class SurveyFunnelReport : IReport
{
    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;

    internal readonly IStringLocalizer S;

    public SurveyFunnelReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        IStringLocalizer<SurveyFunnelReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Funnel;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyFunnelReport>("Completion Funnel");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyFunnelReport>("How many respondents reached each page of a survey, how many left there, and how long they spent on it.");

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

        var reached = new Dictionary<string, int>(StringComparer.Ordinal);
        var left = new Dictionary<string, int>(StringComparer.Ordinal);
        var seconds = new Dictionary<string, List<decimal>>(StringComparer.Ordinal);
        var started = 0;
        var completed = 0;

        await foreach (var response in _queries.ReadAsync(scope.ResultsScope, cancellationToken))
        {
            if (response.Status == SurveyResponseStatus.Voided)
            {
                continue;
            }

            started++;

            if (response.Status == SurveyResponseStatus.Completed && !response.IsTest)
            {
                completed++;
            }

            foreach (var page in (response.PageVisits ?? []).Select(visit => Page(scope, response, visit.PageId)).Distinct(StringComparer.Ordinal))
            {
                reached[page] = reached.GetValueOrDefault(page) + 1;
            }

            foreach (var visit in response.PageVisits ?? [])
            {
                if (visit.LeftUtc is { } leftUtc && leftUtc >= visit.EnteredUtc)
                {
                    var page = Page(scope, response, visit.PageId);

                    if (!seconds.TryGetValue(page, out var list))
                    {
                        seconds[page] = list = [];
                    }

                    list.Add((decimal)(leftUtc - visit.EnteredUtc).TotalSeconds);
                }
            }

            if (response.Status is SurveyResponseStatus.InProgress or SurveyResponseStatus.Abandoned
                && Page(scope, response, response.CurrentPageId ?? response.PageVisits?.LastOrDefault()?.PageId) is { Length: > 0 } last)
            {
                left[last] = left.GetValueOrDefault(last) + 1;
            }
        }

        // The minimum group size counts real, completed responses only: in-progress or test responses can't pad it.
        if (scope.IsTooSmall(completed))
        {
            return _data.TooFewResponses(scope);
        }

        var rows = (scope.Definition?.Pages ?? [])
            .Select((page, index) =>
            {
                var count = reached.GetValueOrDefault(page.Id);
                var median = seconds.TryGetValue(page.Id, out var list) ? SurveyNumberSummary.Median(list.Order().ToList()) : null;

                return new ReportRow(
                [
                    string.IsNullOrWhiteSpace(page.Title) ? S["Page {0}", index + 1].Value : page.Title,
                    ReportFormat.Number(count),
                    started == 0 ? "-" : ReportFormat.Percent((double)count / started),
                    ReportFormat.Number(left.GetValueOrDefault(page.Id)),
                    median is { } value ? ReportFormat.Duration((double)value) : "-",
                ]);
            });

        var table = ReportSection.ForTable(S["Pages"].Value,
            [
                new ReportColumn(S["Page"].Value),
                new ReportColumn(S["Reached"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Share of starts"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Left here"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median time"].Value, ReportColumnAlign.End),
            ],
            rows);
        table.Description = S["Responses started in these days. Left here counts unfinished and abandoned responses whose last page it is."].Value;

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(scope.Survey.Title, [new ReportMetric(S["Started"].Value, ReportFormat.Number(started))]))
            .Add(table);
    }

    // A screen of a page split one question at a time counts for the page it came from.
    private static string Page(SurveyReportScope scope, SurveyResponse response, string pageId)
        => string.IsNullOrEmpty(pageId)
            ? string.Empty
            : response.Plan?.ScreenPages?.GetValueOrDefault(pageId) ?? pageId;
}
