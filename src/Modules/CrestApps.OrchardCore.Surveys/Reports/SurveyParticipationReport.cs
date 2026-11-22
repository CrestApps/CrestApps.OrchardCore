using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The participation of a user audience in an identified survey: who completed it in the days chosen and who has not.
/// It refuses anonymous surveys.
/// </summary>
internal sealed class SurveyParticipationReport : IReport
{
    private const int MaxMembers = 5000;

    private readonly SurveyReportData _data;
    private readonly SurveyResultsQueries _queries;
    private readonly SurveyRespondentNames _names;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public SurveyParticipationReport(
        SurveyReportData data,
        SurveyResultsQueries queries,
        SurveyRespondentNames names,
        IServiceProvider serviceProvider,
        IStringLocalizer<SurveyParticipationReport> stringLocalizer)
    {
        _data = data;
        _queries = queries;
        _names = names;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public static string Name => SurveyReportNames.Participation;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SurveyParticipationReport>("Survey Participation");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SurveyParticipationReport>("Who in a user audience completed an identified survey and who has not yet.");

    public static string Category => SurveyReportNames.Category;

    public static Permission Permission => SurveysPermissions.ViewOwnSurveyResponses;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.AllTime;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _data.ResolveAsync(context, cancellationToken);

        if (scope is null)
        {
            return _data.ChooseSurvey();
        }

        if (scope.IsAnonymous)
        {
            return Message(scope.Survey.Title, S["This survey is anonymous, so it does not record who responded."].Value);
        }

        var audienceName = context.Filter.GetOrDefault<string>(SurveyReportKeys.AudienceId);

        if (string.IsNullOrEmpty(audienceName) || _serviceProvider.GetService<IUserAudienceService>() is not { } audiences)
        {
            return Message(scope.Survey.Title, S["Choose a user audience."].Value);
        }

        var members = new List<string>();
        int count;

        do
        {
            (count, var page) = await audiences.GetMembersAsync(audienceName, members.Count, SurveysConstants.PageSize, cancellationToken);
            members.AddRange(page);

            if (page.Count == 0)
            {
                break;
            }
        }
        while (members.Count < count && members.Count < MaxMembers);

        scope.ResultsScope.CompletedOnly = true;
        var completed = (await _queries.ListRowsAsync(scope.ResultsScope, cancellationToken))
            .Where(row => !string.IsNullOrEmpty(row.UserId))
            .GroupBy(row => row.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(row => row.CompletedUtc), StringComparer.Ordinal);

        var rows = new List<ReportRow>();

        foreach (var member in members.Distinct(StringComparer.Ordinal))
        {
            var done = completed.TryGetValue(member, out var when);

            rows.Add(new ReportRow(
            [
                await _names.GetUserAsync(member, cancellationToken),
                done ? S["Completed"].Value : S["Not yet"].Value,
                done ? await _data.LocalAsync(when) : string.Empty,
            ]));
        }

        var finished = rows.Count(row => completed.Count > 0 && row.Cells[1] == S["Completed"].Value);
        var table = ReportSection.ForTable(S["Members"].Value,
            [
                new ReportColumn(S["Person"].Value),
                new ReportColumn(S["Status"].Value),
                new ReportColumn(S["Last completed"].Value),
            ],
            rows.OrderBy(row => row.Cells[1], StringComparer.CurrentCulture).ThenBy(row => row.Cells[0], StringComparer.CurrentCultureIgnoreCase));
        table.EmptyMessage = S["The audience has no members."].Value;

        if (count > members.Count)
        {
            table.Description = S["The first {0} of {1} members.", members.Count, count].Value;
        }

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(scope.Survey.Title,
            [
                new ReportMetric(S["Members"].Value, ReportFormat.Number(count)),
                new ReportMetric(S["Completed"].Value, ReportFormat.Number(finished)),
                new ReportMetric(S["Participation"].Value, rows.Count == 0 ? "-" : ReportFormat.Percent((double)finished / rows.Count)),
            ]))
            .Add(table);
    }

    private static ReportDocument Message(string title, string message)
        => new ReportDocument().Add(new ReportSection
        {
            Title = title,
            Kind = ReportSectionKind.Metrics,
            EmptyMessage = message,
        });
}
