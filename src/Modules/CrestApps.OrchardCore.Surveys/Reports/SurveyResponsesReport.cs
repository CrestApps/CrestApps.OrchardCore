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
/// The responses of a survey, one row each, with a column per question across the versions read (joined by question id,
/// named as in the newest version). The screen shows the first page; the export holds every response.
/// </summary>
internal sealed class SurveyResponsesReport : IReport
{
    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyRespondentNames _names;

    internal readonly IStringLocalizer S;

    public SurveyResponsesReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        ISurveyControlRegistry registry,
        SurveyRespondentNames names,
        IStringLocalizer<SurveyResponsesReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _registry = registry;
        _names = names;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Responses;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyResponsesReport>("Survey Responses");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyResponsesReport>("Every response to a survey with its answers, one row each, to read or to export to CSV or Excel.");

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

        var columns = SurveyResponseColumns.Build(scope.Versions, _registry);
        var format = new SurveyFormatContext { Culture = CultureInfo.CurrentCulture };
        var limit = context.Mode == ReportRunMode.Export ? int.MaxValue : SurveysConstants.PageSize;
        var header = new List<ReportColumn>
        {
            new(S["Number"].Value),
            new(S["Status"].Value),
        };

        if (scope.CanSeeIdentity)
        {
            header.Add(new ReportColumn(S["Respondent"].Value));
        }

        header.Add(new ReportColumn(S["Version"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Started"].Value));
        header.Add(new ReportColumn(S["Completed"].Value));
        header.Add(new ReportColumn(S["Time taken"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Score"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Percent"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Passed"].Value));
        header.AddRange(columns.Columns.Select(column => new ReportColumn(column.Title ?? column.Key)));

        var rows = new List<ReportRow>();
        var total = 0;

        await foreach (var response in _queries.ReadAsync(scope.ResultsScope, cancellationToken))
        {
            total++;

            if (rows.Count >= limit)
            {
                continue;
            }

            var cells = new List<string>
            {
                response.Number ?? response.ItemId,
                Status(response.Status),
            };

            if (scope.CanSeeIdentity)
            {
                cells.Add(await _names.GetAsync(response.UserId, cancellationToken));
            }

            cells.Add(response.VersionNumber.ToString(CultureInfo.CurrentCulture));
            cells.Add(await _data.LocalAsync(response.StartedUtc));
            cells.Add(await _data.LocalAsync(response.CompletedUtc));
            cells.Add(response.CompletedUtc is { } completed && completed >= response.StartedUtc ? ReportFormat.Duration((completed - response.StartedUtc).TotalSeconds) : string.Empty);
            cells.Add(response.Score is null ? string.Empty : SurveyReportData.Number(response.Score));
            cells.Add(response.Percent is null ? string.Empty : SurveyReportData.Percent(response.Percent));
            cells.Add(response.Passed switch
            {
                true => S["Yes"].Value,
                false => S["No"].Value,
                _ => string.Empty,
            });

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var definition = scope.AllVersions.GetValueOrDefault(response.SurveyVersionId ?? string.Empty)?.Definition;
            format.Definition = definition;
            columns.Write(response, definition, values, format);

            foreach (var column in columns.Columns)
            {
                cells.Add(await CellAsync(values.GetValueOrDefault(column.Key)));
            }

            rows.Add(new ReportRow(cells));
        }

        var table = ReportSection.ForTable(S["Responses"].Value, header, rows);
        table.EmptyMessage = S["No response matches these filters."].Value;

        if (total > rows.Count)
        {
            table.Description = S["The first {0} of {1} responses. Export the report to get them all.", rows.Count, total].Value;
        }

        return new ReportDocument().Add(table);
    }

    private string Status(SurveyResponseStatus status)
        => status switch
        {
            SurveyResponseStatus.InProgress => S["In progress"].Value,
            SurveyResponseStatus.Completed => S["Completed"].Value,
            SurveyResponseStatus.ScreenedOut => S["Screened out"].Value,
            SurveyResponseStatus.Abandoned => S["Abandoned"].Value,
            SurveyResponseStatus.Voided => S["Voided"].Value,
            _ => status.ToString(),
        };

    private async Task<string> CellAsync(object value)
        => value switch
        {
            null => string.Empty,
            DateTime date => await _data.LocalAsync(date),
            decimal number => number.ToString(CultureInfo.CurrentCulture),
            double number => number.ToString(CultureInfo.CurrentCulture),
            long number => number.ToString(CultureInfo.CurrentCulture),
            int number => number.ToString(CultureInfo.CurrentCulture),
            bool flag => flag ? S["Yes"].Value : S["No"].Value,
            _ => Convert.ToString(value, CultureInfo.CurrentCulture),
        };
}
