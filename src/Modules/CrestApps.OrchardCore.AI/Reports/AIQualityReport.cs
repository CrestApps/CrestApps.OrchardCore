using System.Globalization;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Reports;

/// <summary>
/// The QA dashboard: how the AI's scored conversations went in the period, for one QA cohort or all of them. It
/// shows the score, pass and resolution rates, each day, the issues found, the sentiment at the end, the most
/// frequent opening questions and the lowest-scored conversations, each with its AI profile and a link that opens the
/// conversation. It starts on the last 7 days.
/// </summary>
public sealed class AIQualityReport : IReport
{
    /// <summary>
    /// The key the chosen QA cohort is stored under in the report filter.
    /// </summary>
    public const string CohortIdFilterKey = "AIQACohortId";

    /// <summary>
    /// The number of days the report starts on.
    /// </summary>
    public const int DefaultDays = 7;

    // The area of the AI chat admin pages, where a conversation opens. It is named, not referenced, so this module
    // never uses a type of the AI Chat module.
    private const string ChatArea = "CrestApps.OrchardCore.AI.Chat";

    // The format the date range selector of a report posts its bounds in.
    private const string DateRangeFormat = "yyyy-MM-ddTHH:mm";

    private readonly IAIQADashboardService _dashboardService;
    private readonly IAIQACohortStore _cohortStore;
    private readonly IAIProfileStore _profileStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQualityReport"/> class.
    /// </summary>
    /// <param name="dashboardService">Reads the figures.</param>
    /// <param name="cohortStore">The QA cohort store.</param>
    /// <param name="profileStore">The AI profile store, which names the profile of each conversation.</param>
    /// <param name="linkGenerator">Makes the link that opens each conversation.</param>
    /// <param name="httpContextAccessor">The current request, whose tenant the links are on.</param>
    /// <param name="localClock">Converts times to the site time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIQualityReport(
        IAIQADashboardService dashboardService,
        IAIQACohortStore cohortStore,
        IAIProfileStore profileStore,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IStringLocalizer<AIQualityReport> stringLocalizer)
    {
        _dashboardService = dashboardService;
        _cohortStore = cohortStore;
        _profileStore = profileStore;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "ai-qa-dashboard";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIQualityReport>("AI QA Dashboard");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIQualityReport>("How the AI's scored conversations went in the period, for one QA cohort or all of them.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.AIReports;

    /// <summary>
    /// Gets the permission that runs and exports the report.
    /// </summary>
    public static Permission Permission => AIPermissions.ManageAIQuality;

    /// <summary>
    /// Gets the date range the report starts on: the last 7 days, as the QA dashboard did.
    /// </summary>
    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(DefaultDays);

    /// <summary>
    /// Gets the route values that open the report on the last 7 days, for one QA cohort or every scored conversation,
    /// as the QA dashboard opened.
    /// </summary>
    /// <param name="cohortId">The QA cohort, or <see langword="null"/> for every scored conversation.</param>
    /// <param name="localNow">The current date and time in the site's time zone.</param>
    /// <returns>The route values of the report page.</returns>
    public static RouteValueDictionary GetDashboardRouteValues(string cohortId, DateTime localNow)
    {
        var (from, to) = DefaultDateRange.Resolve(localNow);
        var values = new RouteValueDictionary
        {
            ["area"] = ReportsConstants.Feature,
            ["id"] = Name,
            ["From"] = from?.ToString(DateRangeFormat, CultureInfo.InvariantCulture),
            ["To"] = to?.ToString(DateRangeFormat, CultureInfo.InvariantCulture),
            ["Range"] = DefaultDateRange.Key,
        };

        if (!string.IsNullOrEmpty(cohortId))
        {
            values["QACohortId"] = cohortId;
        }

        return values;
    }

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = context.Filter.GetDateRange();
        var cohortId = context.Filter.GetOrDefault<string>(CohortIdFilterKey);
        string label = null;

        if (!string.IsNullOrEmpty(cohortId))
        {
            var cohort = await _cohortStore.FindByIdAsync(cohortId, cancellationToken);

            if (cohort is null)
            {
                return new ReportDocument();
            }

            label = cohort.GetLabel();
        }

        var dashboard = await _dashboardService.BuildAsync(label, range.FromUtc ?? DateTime.MinValue, range.ToUtc ?? DateTime.MaxValue, cancellationToken);

        if (dashboard.Count == 0)
        {
            return context.Mode == ReportRunMode.Export ? new ReportDocument() : NothingScored();
        }

        return Build(
            dashboard,
            await GetProfileNamesAsync(dashboard.Worst, cancellationToken),
            await GetLocalEvaluatedTimesAsync(dashboard.Worst),
            context.Mode == ReportRunMode.Display);
    }

    private async Task<Dictionary<AIConversationEvaluation, DateTime>> GetLocalEvaluatedTimesAsync(IEnumerable<AIConversationEvaluation> evaluations)
    {
        var times = new Dictionary<AIConversationEvaluation, DateTime>(ReferenceEqualityComparer.Instance);

        foreach (var evaluation in evaluations)
        {
            times[evaluation] = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(evaluation.EvaluatedUtc, DateTimeKind.Utc))).DateTime;
        }

        return times;
    }

    private ReportDocument NothingScored()
    {
        var section = ReportSection.ForMetrics(S["Nothing scored yet"].Value, []);
        section.EmptyMessage = S["Conversations appear here once a QA cohort, the Evaluate AI Conversation task or a test scores them."].Value;

        return new ReportDocument().Add(section);
    }

    private async Task<Dictionary<string, string>> GetProfileNamesAsync(IEnumerable<AIConversationEvaluation> evaluations, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var profileId in evaluations.Select(evaluation => evaluation.ProfileId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            var profile = await _profileStore.FindByIdAsync(profileId, cancellationToken);

            names[profileId] = profile is null
                ? profileId
                : string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText;
        }

        return names;
    }

    // The session opens the conversation on the AI chat admin page, which needs its profile.
    private string Session(AIConversationEvaluation evaluation, bool withLink)
    {
        if (!withLink ||
            string.IsNullOrEmpty(evaluation.SessionId) ||
            string.IsNullOrEmpty(evaluation.ProfileId) ||
            _httpContextAccessor.HttpContext is not { } httpContext)
        {
            return evaluation.SessionId;
        }

        var url = _linkGenerator.GetPathByAction(httpContext, "Index", "Admin", new
        {
            area = ChatArea,
            profileId = evaluation.ProfileId,
            sessionId = evaluation.SessionId,
        });

        return ReportValue.Link(evaluation.SessionId, url);
    }

    private ReportDocument Build(
        AIQADashboard dashboard,
        IReadOnlyDictionary<string, string> profileNames,
        IReadOnlyDictionary<AIConversationEvaluation, DateTime> evaluatedTimes,
        bool withLinks)
    {
        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Scored"].Value, ReportFormat.Number(dashboard.Count)),
                new ReportMetric(S["Average score"].Value, dashboard.AverageScore?.ToString("0", CultureInfo.InvariantCulture) ?? "-"),
                new ReportMetric(S["Passed"].Value, Percent(dashboard.PassRate)),
                new ReportMetric(S["Resolved"].Value, Percent(dashboard.ResolutionRate)),
            ]))
            .Add(ReportSection.ForChart(S["Scored by day"].Value, new ReportChart
            {
                Type = ReportChartType.Bar,
                ShowLegend = false,
                Labels = dashboard.Days.Select(day => day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList(),
                Datasets = [new ReportChartDataset(S["Scored"].Value, dashboard.Days.Select(day => (double)day.Count))],
            }, width: 12))
            .Add(ReportSection.ForTable(S["By day"].Value,
            [
                new ReportColumn(S["Day"].Value),
                new ReportColumn(S["Scored"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Passed"].Value, ReportColumnAlign.End),
            ],
            dashboard.Days.Select(day => new ReportRow(
            [
                day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReportFormat.Number(day.Count),
                day.AverageScore.ToString("0", CultureInfo.InvariantCulture),
                ReportFormat.Percent(day.PassRate),
            ]))));

        var maxIssues = dashboard.Issues.Count == 0 ? 1 : dashboard.Issues.Max(issue => issue.Count);
        var issues = ReportSection.ForBars(S["Issues found"].Value, dashboard.Issues.Select(issue => new ReportBar(issue.Kind, ReportFormat.Number(issue.Count), (double)issue.Count / maxIssues)));
        issues.Width = 6;

        if (dashboard.Issues.Count == 0)
        {
            issues.Description = S["None."].Value;
        }

        var maxSentiment = dashboard.Sentiments.Count == 0 ? 1 : dashboard.Sentiments.Values.Max();
        var sentiments = ReportSection.ForBars(S["Sentiment at the end"].Value, dashboard.Sentiments
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ReportBar(entry.Key, ReportFormat.Number(entry.Value), (double)entry.Value / maxSentiment)));
        sentiments.Width = 6;

        document
            .Add(issues)
            .Add(sentiments)
            .Add(ReportSection.ForTable(S["Top questions"].Value,
            [
                new ReportColumn(S["Question"].Value),
                new ReportColumn(S["Asked"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Resolved"].Value, ReportColumnAlign.End),
            ],
            dashboard.TopQuestions.Select(question => new ReportRow(
            [
                question.Example,
                ReportFormat.Number(question.Count),
                ReportFormat.Percent(question.ResolutionRate),
            ]))))
            .Add(LowestScores(dashboard, profileNames, evaluatedTimes, withLinks));

        return document;
    }

    private ReportSection LowestScores(
        AIQADashboard dashboard,
        IReadOnlyDictionary<string, string> profileNames,
        IReadOnlyDictionary<AIConversationEvaluation, DateTime> evaluatedTimes,
        bool withLinks)
    {
        var section = ReportSection.ForTable(S["Lowest scores"].Value,
            [
                new ReportColumn(S["Score"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Passed"].Value),
                new ReportColumn(S["Evaluated"].Value),
                new ReportColumn(S["Session"].Value),
                new ReportColumn(S["AI profile"].Value),
                new ReportColumn(S["Summary"].Value),
            ],
            dashboard.Worst.Select(evaluation => new ReportRow(
            [
                evaluation.Score.ToString(CultureInfo.InvariantCulture),
                evaluation.Passed ? S["Yes"].Value : S["No"].Value,
                evaluatedTimes[evaluation].ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Session(evaluation, withLinks),
                string.IsNullOrEmpty(evaluation.ProfileId) ? null : profileNames.GetValueOrDefault(evaluation.ProfileId, evaluation.ProfileId),
                evaluation.Summary,
            ]).WithCellStyle(0, ReportStyle.Create(evaluation.Passed ? "#15803D" : "#B91C1C", bold: true))));

        section.Description = S["Open a conversation from its session."].Value;

        return section;
    }

    private static string Percent(double? rate)
        => rate is { } value ? ReportFormat.Percent(value) : "-";
}
