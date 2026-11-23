using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The responses of a survey, one row each, with a column per question across the versions read (joined by question id,
/// named as in the newest version). The screen shows one page, chosen in the filter; the export holds every response.
/// Completed responses are listed unless the filter asks for other statuses. An anonymous survey follows the shared
/// small-group rule of <see cref="SurveyGroupGate"/> (as the responses list does), labels its rows with no response
/// number, reads them in random-id order and shows its times by the day only. Scores below the pass mark,
/// failed responses and Net Promoter Score answers are colored with the shared report colorizer.
/// </summary>
internal sealed class SurveyResponsesReport : IReport
{
    /// <summary>
    /// The rows a page of the screen shows.
    /// </summary>
    public const int ScreenPageSize = 100;

    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyRespondentNames _names;
    private readonly SurveyAnonymousGroups _groups;

    internal readonly IStringLocalizer S;

    public SurveyResponsesReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        ISurveyControlRegistry registry,
        SurveyRespondentNames names,
        SurveyAnonymousGroups groups,
        IStringLocalizer<SurveyResponsesReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _registry = registry;
        _names = names;
        _groups = groups;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Responses;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyResponsesReport>("Survey Responses");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyResponsesReport>("Every response to a survey with its answers, one row each, to read or to export to CSV or Excel.");

    public static string Category => SurveyReportNames.Category;

    public static Permission Permission => SurveysPermissions.ViewOwnSurveyResponses;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    /// <summary>
    /// Gets the statuses a status filter value lists.
    /// </summary>
    /// <param name="value">The filter value, or <see langword="null"/> for completed responses.</param>
    /// <returns>The statuses, or empty for every status.</returns>
    public static List<SurveyResponseStatus> StatusesOf(string value)
        => value switch
        {
            SurveyReportKeys.AllStatuses => [],
            SurveyReportKeys.FinishedStatus => [SurveyResponseStatus.Completed, SurveyResponseStatus.ScreenedOut],
            _ when Enum.TryParse<SurveyResponseStatus>(value, ignoreCase: true, out var status) => [status],
            _ => [SurveyResponseStatus.Completed],
        };

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _data.ResolveAsync(context, cancellationToken);

        if (scope is null)
        {
            return _data.ChooseSurvey();
        }

        scope.ResultsScope.Statuses = StatusesOf(context.Filter.GetOrDefault<string>(SurveyReportKeys.Status));

        // The shared small-group rule of the responses list (SurveyGroupGate), counted on the narrowing filters: nothing
        // shows until the completed, non-test responses reach the minimum group size, and a status too few real responses
        // share is left out (all of the report when it is the status filtered to), so filtering to Abandoned, or reading
        // every status, can't single out the one person who gave up.
        if (scope.IsAnonymous)
        {
            var decision = await _groups.DecideAsync(scope.Survey, scope.ResultsScope, scope.ResultsScope.Statuses, cancellationToken);

            if (decision.HidesAll)
            {
                return _data.TooFewResponses(scope);
            }

            scope.ResultsScope.ExcludedStatuses = decision.HiddenStatuses.ToList();

            // The order rows were stored in follows when people started, so an anonymous survey reads by random id.
            scope.ResultsScope.OrderById = true;
        }

        var total = await _queries.CountAsync(scope.ResultsScope, cancellationToken);

        var export = context.Mode == ReportRunMode.Export;
        var pages = Math.Max(1, (total + ScreenPageSize - 1) / ScreenPageSize);
        var page = Math.Clamp(context.Filter.GetOrDefault<int?>(SurveyReportKeys.Page) ?? 1, 1, pages);
        var skip = export ? 0 : (page - 1) * ScreenPageSize;
        var take = export ? int.MaxValue : ScreenPageSize;

        var columns = SurveyResponseColumns.Build(scope.Versions, _registry);
        var format = new SurveyFormatContext { Culture = CultureInfo.CurrentCulture };
        var showRespondent = scope.IsAnonymous || scope.CanSeeIdentity;
        var header = new List<ReportColumn>
        {
            new(S["Number"].Value),
            new(S["Status"].Value),
        };

        if (showRespondent)
        {
            header.Add(new ReportColumn(S["Respondent"].Value));
        }

        header.Add(new ReportColumn(S["Version"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Started"].Value));
        header.Add(new ReportColumn(S["Completed"].Value));
        header.Add(new ReportColumn(S["Time taken"].Value, ReportColumnAlign.End));

        var scoreIndex = header.Count;

        header.Add(new ReportColumn(S["Score"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Percent"].Value, ReportColumnAlign.End));
        header.Add(new ReportColumn(S["Passed"].Value));

        var answersIndex = header.Count;

        header.AddRange(columns.Columns.Select(column => new ReportColumn(column.Title ?? column.Key)));

        var today = await _data.NowAsync();
        var passMark = scope.Definition?.Scoring?.PassMarkPercent;
        var belowPass = passMark is { } mark ? ReportColumnColorizer.Create(DataFieldType.Decimal, SurveyReportColors.BelowPass(mark), null, today) : null;
        var failed = ReportColumnColorizer.Create(DataFieldType.Boolean, SurveyReportColors.Failed(), null, today);
        var answerColors = AnswerColorizers(scope, columns, today);
        var yes = S["Yes"].Value;
        var no = S["No"].Value;
        var rows = new List<ReportRow>();

        await foreach (var response in _queries.ReadAsync(scope.ResultsScope, skip, take, cancellationToken))
        {
            var cells = new List<string>
            {
                SurveyAnonymity.ResponseLabel(scope.Survey, response),
                Status(response.Status),
            };

            if (showRespondent)
            {
                cells.Add(await _names.GetAsync(response, scope.Survey, cancellationToken));
            }

            cells.Add(response.VersionNumber.ToString(CultureInfo.CurrentCulture));
            cells.Add(scope.IsAnonymous ? await _data.LocalDayAsync(response.StartedUtc) : await _data.LocalAsync(response.StartedUtc));
            cells.Add(scope.IsAnonymous ? await _data.LocalDayAsync(response.CompletedUtc) : await _data.LocalAsync(response.CompletedUtc));
            cells.Add(response.CompletedUtc is { } completed && completed >= response.StartedUtc ? ReportFormat.Duration((completed - response.StartedUtc).TotalSeconds) : string.Empty);
            cells.Add(response.Score is null ? string.Empty : SurveyReportData.Number(response.Score));
            cells.Add(response.Percent is null ? string.Empty : SurveyReportData.Percent(response.Percent));
            cells.Add(response.Passed switch
            {
                true => yes,
                false => no,
                _ => string.Empty,
            });

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var definition = scope.AllVersions.GetValueOrDefault(response.SurveyVersionId ?? string.Empty)?.Definition;
            format.Definition = definition;
            columns.Write(response, definition, values, format);

            foreach (var column in columns.Columns)
            {
                cells.Add(SurveyReportCells.Format(values.GetValueOrDefault(column.Key), column.Type, CultureInfo.CurrentCulture, yes, no));
            }

            var row = new ReportRow(cells);

            if (belowPass?.StyleOf(response.Percent) is { } belowStyle)
            {
                row.WithCellStyle(scoreIndex, belowStyle).WithCellStyle(scoreIndex + 1, belowStyle);
            }

            if (response.Passed is not null && failed?.StyleOf(response.Passed) is { } failedStyle)
            {
                row.WithCellStyle(scoreIndex + 2, failedStyle);
            }

            for (var index = 0; index < columns.Columns.Count; index++)
            {
                if (answerColors[index]?.StyleOf(values.GetValueOrDefault(columns.Columns[index].Key)) is { } style)
                {
                    row.WithCellStyle(answersIndex + index, style);
                }
            }

            rows.Add(row);
        }

        var table = ReportSection.ForTable(S["Responses"].Value, header, rows);
        table.EmptyMessage = S["No response matches these filters."].Value;
        table.Description = Describe(scope, columns, total, page, pages, skip, rows.Count, export);

        return new ReportDocument().Add(table);
    }

    private static ReportColumnColorizer[] AnswerColorizers(SurveyReportScope scope, SurveyResponseColumns columns, DateTime today)
    {
        var nps = new HashSet<string>(StringComparer.Ordinal);

        foreach (var version in scope.Versions)
        {
            foreach (var (_, question) in SurveyDefinitionQuestions.All(version.Definition))
            {
                if (RatingAnalyzer.IsNps(question))
                {
                    nps.Add(question.Id);
                }
            }
        }

        return columns.Columns
            .Select(column => column.QuestionId is not null && nps.Contains(column.QuestionId) && column.RowId is null && column.Type is DataFieldType.Integer or DataFieldType.Decimal
                ? ReportColumnColorizer.Create(column.Type, SurveyReportColors.Nps(), null, today)
                : null)
            .ToArray();
    }

    private string Describe(SurveyReportScope scope, SurveyResponseColumns columns, int total, int page, int pages, int skip, int shown, bool export)
    {
        var notes = new List<string>();

        if (!export && total > shown)
        {
            notes.Add(S["Responses {0} to {1} of {2} (page {3} of {4}). Choose another page in the filter, or export the report to get them all.", skip + 1, skip + shown, total, page, pages].Value);
        }

        if (scope.IsAnonymous)
        {
            notes.Add(S["This survey is anonymous: nobody is named, responses show a code instead of a number, and times show the day only."].Value);

            if (scope.ResultsScope.ExcludedStatuses.Count > 0)
            {
                notes.Add(S["Responses of a status fewer than {0} responses share are left out so no one can be recognized.", scope.MinimumGroupSize].Value);
            }
        }

        if (columns.MissingQuestions.Count > 0)
        {
            notes.Add(S["These questions have no column because their question type is turned off: {0}.", string.Join(", ", columns.MissingQuestions)].Value);
        }

        return notes.Count == 0 ? null : string.Join(" ", notes);
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
}
