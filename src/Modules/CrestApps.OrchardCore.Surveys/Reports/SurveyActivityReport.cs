using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The activity of the surveys a person may read: completed responses per survey in the days chosen, and the responses
/// waiting for a grader. The Survey Activity dashboard widget shows it. The counts of every survey come from two grouped
/// statements, and only the surveys with activity are looked up and authorized, so the cost does not grow with the
/// number of surveys (every Learning quiz owns one).
/// </summary>
internal sealed class SurveyActivityReport : IReport
{
    private readonly SurveyReportData _data;
    private readonly SurveyActivityCounts _counts;
    private readonly ISurveyManager _surveyManager;

    internal readonly IStringLocalizer S;

    public SurveyActivityReport(
        SurveyReportData data,
        ISession session,
        ISurveyManager surveyManager,
        IStringLocalizer<SurveyActivityReport> stringLocalizer)
    {
        _data = data;
        _counts = new SurveyActivityCounts(session);
        _surveyManager = surveyManager;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Activity;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyActivityReport>("Survey Activity");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyActivityReport>("Completed responses per survey in the days chosen, and the responses waiting for a grader.");

    public static string Category => SurveyReportNames.Category;

    public static Permission Permission => SurveysPermissions.ViewOwnSurveyResponses;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(7);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = context.Filter.GetDateRange();
        var to = range.ToUtc is { } end && end.Ticks < DateTime.MaxValue.Ticks ? end.AddTicks(1) : (DateTime?)null;
        var rows = new List<(string Title, long Completed, long Pending)>();

        foreach (var (surveyId, counts) in await _counts.CountBySurveyAsync(range.FromUtc, to, cancellationToken))
        {
            if (counts.Completed <= 0 && counts.Pending <= 0)
            {
                continue;
            }

            var survey = await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

            if (survey is null || survey.PublishedVersionNumber <= 0 || !await _data.CanViewResponsesAsync(context.User, survey, cancellationToken))
            {
                continue;
            }

            rows.Add((survey.Title ?? survey.Name, counts.Completed, counts.Pending));
        }

        var table = ReportSection.ForTable(S["Surveys"].Value,
            [
                new ReportColumn(S["Survey"].Value),
                new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Waiting for a grader"].Value, ReportColumnAlign.End),
            ],
            rows.OrderByDescending(row => row.Completed).Select(row => new ReportRow([row.Title, ReportFormat.Number(row.Completed), ReportFormat.Number(row.Pending)])));
        table.EmptyMessage = S["No survey was answered in these days."].Value;

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Surveys"].Value,
            [
                new ReportMetric(S["Completed responses"].Value, ReportFormat.Number(rows.Sum(row => row.Completed))),
                new ReportMetric(S["Waiting for a grader"].Value, ReportFormat.Number(rows.Sum(row => row.Pending))),
            ]))
            .Add(table);
    }
}
