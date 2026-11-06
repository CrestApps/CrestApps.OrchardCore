using System.Globalization;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The AI chat session analytics: sessions, visitors, containment, abandonment, handle time, when sessions start,
/// who starts them, response latency and tokens, resolution, conversion and feedback, for the sessions that started
/// in the period, of one AI profile or all of them. It starts on every session. Its export is one flat table with a
/// row per session, with the invariant columns of the analytics page's CSV, and needs the export permission.
/// </summary>
public sealed class AIChatSessionAnalyticsReport : IReport, IReportFilterMetadata
{
    // The columns of the export, by their invariant names, as the analytics page exported them.
    private static readonly string[] _sessionColumns =
    [
        "SessionId",
        "ProfileId",
        "VisitorId",
        "UserId",
        "IsAuthenticated",
        "SessionStartedUtc",
        "SessionEndedUtc",
        "MessageCount",
        "HandleTimeSeconds",
        "IsResolved",
    ];

    private readonly IAIChatSessionEventReader _reader;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionAnalyticsReport"/> class.
    /// </summary>
    /// <param name="reader">Reads the session events.</param>
    /// <param name="localClock">The local clock, for the site's time zone.</param>
    /// <param name="clock">The clock, for converting session starts to the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatSessionAnalyticsReport(
        IAIChatSessionEventReader reader,
        ILocalClock localClock,
        IClock clock,
        IStringLocalizer<AIChatSessionAnalyticsReport> stringLocalizer)
    {
        _reader = reader;
        _localClock = localClock;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "ai-chat-session-analytics";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIChatSessionAnalyticsReport>("AI Chat Session Analytics");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIChatSessionAnalyticsReport>("Sessions, visitors, containment, abandonment, handle time, feedback and conversion of the AI chat sessions that started in the period.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.AIReports;

    /// <summary>
    /// Gets the permission that runs the report.
    /// </summary>
    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

    /// <summary>
    /// Gets the permission that exports the report, beside the one that runs it.
    /// </summary>
    public static Permission ExportPermission => ChatAnalyticsPermissionProvider.ExportChatAnalytics;

    /// <summary>
    /// Gets the date range the report starts on: every session, as the analytics page did.
    /// </summary>
    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.AllTime;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FilterNames { get; } = [AIChatReportFilters.Profile];

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = context.Filter.GetDateRange();
        var events = await _reader.ListAsync(new AIChatSessionEventQuery
        {
            ProfileId = context.Filter.GetOrDefault<string>(AIChatReportFilters.ProfileId),
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
        }, cancellationToken);

        // An export is one flat table with a row per session, as scripts read it, even when it has no rows.
        if (context.Mode == ReportRunMode.Export)
        {
            return new ReportDocument().Add(Sessions(events));
        }

        if (events.Count == 0)
        {
            return new ReportDocument();
        }

        var timeZone = await _localClock.GetLocalTimeZoneAsync();

        DateTime ToLocal(DateTime utc)
            => _clock.ConvertToTimeZone(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), timeZone).DateTime;

        return new ReportDocument()
            .Add(Overview(events))
            .Add(TimeOfDay(events, ToLocal))
            .Add(DayOfWeek(events, ToLocal))
            .Add(UserSegments(events))
            .Add(Performance(events))
            .Add(Conversion(events))
            .Add(Feedback(events));
    }

    private ReportSection Overview(IReadOnlyList<AIChatSessionEvent> events)
    {
        var resolved = events.Count(evt => evt.IsResolved);
        var abandoned = events.Count(evt => !evt.IsResolved && evt.SessionEndedUtc.HasValue);
        var visitors = events
            .Where(evt => !string.IsNullOrEmpty(evt.VisitorId))
            .GroupBy(evt => evt.VisitorId, StringComparer.Ordinal)
            .ToList();
        var withHandleTime = events.Where(evt => evt.HandleTimeSeconds > 0).ToList();
        var resolvedEvents = events.Where(evt => evt.IsResolved).ToList();

        var section = ReportSection.ForMetrics(S["Overview"].Value,
        [
            new ReportMetric(S["Sessions"].Value, ReportFormat.Number(events.Count))
                .WithDescription(S["The total number of chat conversations started within the selected period."].Value),
            new ReportMetric(S["Unique visitors"].Value, ReportFormat.Number(visitors.Count))
                .WithDescription(S["The number of distinct visitors who interacted with the chat, identified by visitor ID or user account."].Value),
            new ReportMetric(S["Resolved"].Value, ReportFormat.Number(resolved))
                .WithDescription(S["The number of sessions that ended with a natural resolution (the bot successfully handled the user's request)."].Value),
            new ReportMetric(S["Abandoned"].Value, ReportFormat.Number(abandoned), S["Ended without being resolved."].Value)
                .WithDescription(S["The number of sessions that ended due to inactivity timeout without the user reaching a resolution."].Value),
            new ReportMetric(S["Active"].Value, ReportFormat.Number(events.Count(evt => !evt.SessionEndedUtc.HasValue)))
                .WithDescription(S["The number of sessions that are currently in progress and have not yet ended."].Value),
            new ReportMetric(S["Containment rate"].Value, ReportFormat.Percent((double)resolved / events.Count))
                .WithDescription(S["The percentage of sessions that were fully resolved by the bot without requiring human escalation. Higher is better."].Value),
            new ReportMetric(S["Abandonment rate"].Value, ReportFormat.Percent((double)abandoned / events.Count))
                .WithDescription(S["The percentage of sessions where the user left the conversation without reaching a resolution. This may indicate user frustration or irrelevant responses."].Value),
            new ReportMetric(S["Average handle time"].Value, withHandleTime.Count > 0 ? ReportFormat.Duration(withHandleTime.Average(evt => evt.HandleTimeSeconds)) : "-")
                .WithDescription(S["The average time from the first message to the last message in a session. Measures how long users typically interact with the bot."].Value),
            new ReportMetric(S["Messages per session"].Value, Decimal(events.Average(evt => evt.MessageCount)))
                .WithDescription(S["The average number of messages (user + assistant) exchanged per session. Indicates conversation depth and engagement."].Value),
            new ReportMetric(S["Returning visitors"].Value, visitors.Count > 0 ? ReportFormat.Percent((double)visitors.Count(group => group.Count() > 1) / visitors.Count) : "-", S["Visitors with more than one session."].Value)
                .WithDescription(S["The percentage of unique visitors who engaged with the bot in more than one session. A high rate indicates users find the bot useful enough to come back."].Value),
            new ReportMetric(S["Messages to resolution"].Value, resolvedEvents.Count > 0 ? Decimal(resolvedEvents.Average(evt => evt.MessageCount)) : "-")
                .WithDescription(S["The average number of messages needed to reach a resolution in resolved sessions. Fewer steps means faster problem-solving."].Value),
        ]);

        section.Description = S["How many conversations started in the period, how they ended, and how engaged the visitors were."].Value;

        return section;
    }

    private ReportSection TimeOfDay(IReadOnlyList<AIChatSessionEvent> events, Func<DateTime, DateTime> toLocal)
    {
        var counts = new double[24];

        foreach (var evt in events)
        {
            counts[toLocal(evt.SessionStartedUtc).Hour]++;
        }

        var section = ReportSection.ForChart(S["Sessions by time of day"].Value, new ReportChart
        {
            Type = ReportChartType.Bar,
            ShowLegend = false,
            Labels = Enumerable.Range(0, 24).Select(HourLabel).ToList(),
            Datasets = [new ReportChartDataset(S["Sessions"].Value, counts)],
        });

        section.Description = S["Shows how chat sessions are distributed across the hours of the day, in the site's time zone. Helps identify peak usage times for staffing and resource allocation."].Value;

        return section;
    }

    private ReportSection DayOfWeek(IReadOnlyList<AIChatSessionEvent> events, Func<DateTime, DateTime> toLocal)
    {
        var counts = new double[7];

        foreach (var evt in events)
        {
            counts[(int)toLocal(evt.SessionStartedUtc).DayOfWeek]++;
        }

        var section = ReportSection.ForChart(S["Sessions by day of week"].Value, new ReportChart
        {
            Type = ReportChartType.Bar,
            ShowLegend = false,
            Labels = CultureInfo.CurrentCulture.DateTimeFormat.DayNames.ToList(),
            Datasets = [new ReportChartDataset(S["Sessions"].Value, counts)],
        });

        section.Description = S["Shows how chat sessions are distributed across the days of the week. Helps identify peak usage days for capacity planning."].Value;

        return section;
    }

    private ReportSection UserSegments(IReadOnlyList<AIChatSessionEvent> events)
    {
        var authenticated = events.Count(evt => evt.IsAuthenticated);

        var section = ReportSection.ForMetrics(S["Who chats"].Value,
        [
            new ReportMetric(S["Authenticated sessions"].Value, ReportFormat.Number(authenticated), ReportFormat.Percent((double)authenticated / events.Count))
                .WithDescription(S["The number of chat sessions initiated by logged-in users with a known user account."].Value),
            new ReportMetric(S["Anonymous sessions"].Value, ReportFormat.Number(events.Count - authenticated), ReportFormat.Percent((double)(events.Count - authenticated) / events.Count))
                .WithDescription(S["The number of chat sessions from visitors who were not logged in. These are tracked by a browser-generated visitor ID."].Value),
            new ReportMetric(S["Authenticated users"].Value, ReportFormat.Number(events.Where(evt => evt.IsAuthenticated && !string.IsNullOrEmpty(evt.UserId)).Select(evt => evt.UserId).Distinct(StringComparer.Ordinal).Count()))
                .WithDescription(S["The number of distinct authenticated user accounts that initiated chat sessions."].Value),
            new ReportMetric(S["Anonymous visitors"].Value, ReportFormat.Number(events.Where(evt => !evt.IsAuthenticated && !string.IsNullOrEmpty(evt.VisitorId)).Select(evt => evt.VisitorId).Distinct(StringComparer.Ordinal).Count()))
                .WithDescription(S["The number of distinct anonymous visitors identified by their browser-generated visitor ID."].Value),
        ]);

        section.Description = S["Breakdown of chat sessions by user authentication status. Shows how many sessions come from logged-in users versus anonymous visitors."].Value;

        return section;
    }

    private ReportSection Performance(IReadOnlyList<AIChatSessionEvent> events)
    {
        var withLatency = events.Where(evt => evt.AverageResponseLatencyMs > 0).ToList();
        var withTokens = events.Where(evt => evt.TotalInputTokens > 0 || evt.TotalOutputTokens > 0).ToList();
        var input = withTokens.Sum(evt => (long)evt.TotalInputTokens);
        var output = withTokens.Sum(evt => (long)evt.TotalOutputTokens);
        var tokensMeasured = S["{0} sessions measured", withTokens.Count].Value;
        var metrics = new List<ReportMetric>();

        if (withLatency.Count > 0 || withTokens.Count > 0)
        {
            metrics.AddRange(
            [
                new ReportMetric(S["Average response latency"].Value, withLatency.Count > 0 ? S["{0} ms", Math.Round(withLatency.Average(evt => evt.AverageResponseLatencyMs)).ToString("N0", CultureInfo.CurrentCulture)].Value : "-", S["{0} sessions measured", withLatency.Count].Value)
                    .WithDescription(S["The average time (in milliseconds) the AI model takes to generate a complete response. Lower latency means faster responses for users."].Value),
                new ReportMetric(S["Input tokens"].Value, ReportFormat.Number(input))
                    .WithDescription(S["The total number of input (prompt) tokens sent to the AI model. This includes the conversation history, system instructions, and user messages."].Value),
                new ReportMetric(S["Output tokens"].Value, ReportFormat.Number(output))
                    .WithDescription(S["The total number of output (completion) tokens generated by the AI model. These are the tokens in the assistant's responses."].Value),
                new ReportMetric(S["Total tokens"].Value, ReportFormat.Number(input + output))
                    .WithDescription(S["The total number of tokens (input + output) consumed across all sessions. Tokens are the basic units of text that AI models process, and token usage directly affects API costs."].Value),
                new ReportMetric(S["Tokens per session"].Value, PerSession(input + output, withTokens.Count), tokensMeasured)
                    .WithDescription(S["The average total tokens consumed per session. Useful for estimating per-conversation costs and monitoring usage efficiency."].Value),
                new ReportMetric(S["Input tokens per session"].Value, PerSession(input, withTokens.Count), tokensMeasured)
                    .WithDescription(S["The average input tokens per session. High values may indicate lengthy conversation histories or verbose system prompts."].Value),
                new ReportMetric(S["Output tokens per session"].Value, PerSession(output, withTokens.Count), tokensMeasured)
                    .WithDescription(S["The average output tokens per session. Indicates how verbose the AI responses are on average."].Value),
            ]);
        }

        var section = ReportSection.ForMetrics(S["Performance"].Value, metrics);

        section.Description = S["Metrics about the AI model's response speed and resource consumption. These metrics are captured when the AI provider returns token usage and latency data."].Value;
        section.EmptyMessage = S["No performance data available. Performance metrics are captured when the AI provider returns token usage and latency information."].Value;

        return section;
    }

    private ReportSection Conversion(IReadOnlyList<AIChatSessionEvent> events)
    {
        var closed = events.Where(evt => evt.SessionEndedUtc.HasValue).ToList();
        var scored = events
            .Where(evt => evt.ConversionScore.HasValue && evt.ConversionMaxScore.HasValue && evt.ConversionMaxScore.Value > 0)
            .ToList();
        var metrics = new List<ReportMetric>();

        if (closed.Count > 0)
        {
            metrics.Add(new ReportMetric(S["AI resolved"].Value, ReportFormat.Number(closed.Count(evt => evt.IsResolved)))
                .WithDescription(S["Sessions where semantic analysis determined the user's query or task was successfully addressed, regardless of how the session ended."].Value));
            metrics.Add(new ReportMetric(S["AI unresolved"].Value, ReportFormat.Number(closed.Count(evt => !evt.IsResolved)))
                .WithDescription(S["Sessions where semantic analysis determined the user's needs were not fully met. Review these to identify areas for improvement."].Value));
            metrics.Add(new ReportMetric(S["AI resolution rate"].Value, ReportFormat.Percent((double)closed.Count(evt => evt.IsResolved) / closed.Count), S["Of the sessions that ended."].Value)
                .WithDescription(S["The percentage of closed sessions that were determined to be semantically resolved. Unlike simple timeout-based detection, this uses semantic analysis of the conversation content to determine if the user's needs were met."].Value));
        }

        if (scored.Count > 0)
        {
            var score = scored.Sum(evt => evt.ConversionScore!.Value);
            var maxScore = scored.Sum(evt => evt.ConversionMaxScore!.Value);
            var high = scored.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value >= 0.7);
            var low = scored.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value < 0.3);

            metrics.Add(new ReportMetric(S["Sessions scored"].Value, ReportFormat.Number(scored.Count))
                .WithDescription(S["The number of sessions that were evaluated against the configured conversion goals."].Value));
            metrics.Add(new ReportMetric(S["Average conversion score"].Value, ReportFormat.Percent((double)score / maxScore), S["{0} of {1} points", score, maxScore].Value)
                .WithDescription(S["The average conversion score as a percentage of the maximum possible score across all evaluated sessions. Based on AI evaluation of configured conversion goals."].Value));
            metrics.Add(new ReportMetric(S["High performing"].Value, ReportFormat.Number(high), S["70% of the points or more; {0}", ReportFormat.Percent((double)high / scored.Count)].Value)
                .WithDescription(S["Evaluated sessions that scored 70% or higher on conversion goals. These sessions are meeting or exceeding expectations."].Value));
            metrics.Add(new ReportMetric(S["Low performing"].Value, ReportFormat.Number(low), S["Under 30% of the points; {0}", ReportFormat.Percent((double)low / scored.Count)].Value)
                .WithDescription(S["Evaluated sessions that scored below 30% on conversion goals. These sessions need attention and may indicate configuration or response quality issues."].Value));
        }

        var section = ReportSection.ForMetrics(S["Resolution and conversion"].Value, metrics);

        section.Description = S["Metrics based on resolution detection and goal-based conversion scoring configured per profile."].Value;
        section.EmptyMessage = S["No resolution or conversion data available. Enable Resolution Detection and/or Conversion Metrics in your profile analytics settings to start collecting this data."].Value;

        return section;
    }

    private ReportSection Feedback(IReadOnlyList<AIChatSessionEvent> events)
    {
        var up = events.Sum(evt => evt.ThumbsUpCount);
        var down = events.Sum(evt => evt.ThumbsDownCount);
        var ratings = up + down;
        var withFeedback = events.Count(evt => evt.ThumbsUpCount > 0 || evt.ThumbsDownCount > 0);
        var metrics = new List<ReportMetric>();

        if (ratings > 0)
        {
            metrics.AddRange(
            [
                new ReportMetric(S["Thumbs up"].Value, ReportFormat.Number(up), ReportFormat.Percent((double)up / ratings))
                    .WithDescription(S["The total number of thumbs-up ratings given on individual messages across all sessions. Its percentage is the satisfaction rate: the positive ratings out of all ratings."].Value),
                new ReportMetric(S["Thumbs down"].Value, ReportFormat.Number(down), ReportFormat.Percent((double)down / ratings))
                    .WithDescription(S["The total number of thumbs-down ratings given on individual messages across all sessions. Review these sessions to identify improvement areas."].Value),
                new ReportMetric(S["Sessions without feedback"].Value, ReportFormat.Number(events.Count - withFeedback))
                    .WithDescription(S["The number of sessions in which no message was rated."].Value),
                new ReportMetric(S["Feedback rate"].Value, ReportFormat.Percent((double)withFeedback / events.Count), S["Sessions with at least one rating."].Value)
                    .WithDescription(S["The percentage of total sessions where users provided a rating. A low feedback rate may indicate users aren't being prompted to rate, or they find the rating mechanism inconvenient."].Value),
            ]);
        }

        var section = ReportSection.ForMetrics(S["Feedback"].Value, metrics);

        section.Description = S["Summary of user satisfaction ratings collected during chat sessions. Users can rate their experience with thumbs up or thumbs down."].Value;
        section.EmptyMessage = S["No feedback data available. Feedback metrics are collected when users rate their chat experience using thumbs up/down buttons."].Value;

        return section;
    }

    // The flat table of the export: invariant column names and values, as the analytics page's CSV had them.
    private static ReportSection Sessions(IReadOnlyList<AIChatSessionEvent> events)
        => ReportSection.ForTable("Sessions",
            _sessionColumns.Select(column => new ReportColumn(column, column is "MessageCount" or "HandleTimeSeconds" ? ReportColumnAlign.End : ReportColumnAlign.Start)),
            events.Select(evt => new ReportRow(
            [
                evt.SessionId,
                evt.ProfileId,
                evt.VisitorId,
                evt.UserId,
                evt.IsAuthenticated.ToString(),
                evt.SessionStartedUtc.ToString("o", CultureInfo.InvariantCulture),
                evt.SessionEndedUtc?.ToString("o", CultureInfo.InvariantCulture),
                evt.MessageCount.ToString(CultureInfo.InvariantCulture),
                evt.HandleTimeSeconds.ToString(CultureInfo.InvariantCulture),
                evt.IsResolved.ToString(),
            ])));

    // A culture that writes a 24-hour clock, or has no AM/PM designators, would otherwise read the same hour twice.
    private static string HourLabel(int hour)
    {
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        var time = new DateTime(2000, 1, 1, hour, 0, 0, DateTimeKind.Unspecified);

        return format.ShortTimePattern.Contains('H', StringComparison.Ordinal) ||
            string.IsNullOrEmpty(format.AMDesignator) ||
            string.IsNullOrEmpty(format.PMDesignator)
            ? time.ToString("HH:mm", CultureInfo.CurrentCulture)
            : time.ToString("h tt", CultureInfo.CurrentCulture);
    }

    private static string PerSession(long tokens, int sessions)
        => sessions > 0 ? ReportFormat.Number((long)Math.Round((double)tokens / sessions)) : "-";

    private static string Decimal(double value)
        => value.ToString("0.0", CultureInfo.InvariantCulture);
}
