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
/// in the period, of one AI profile or all of them. The export adds a row per session.
/// </summary>
public sealed class AIChatSessionAnalyticsReport : IReport, IReportFilterMetadata
{
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
    /// Gets the permission that runs and exports the report.
    /// </summary>
    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

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

        if (events.Count == 0)
        {
            return new ReportDocument();
        }

        var timeZone = await _localClock.GetLocalTimeZoneAsync();

        DateTime ToLocal(DateTime utc)
            => _clock.ConvertToTimeZone(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), timeZone).DateTime;

        var document = new ReportDocument()
            .Add(Overview(events))
            .Add(TimeOfDay(events, ToLocal))
            .Add(DayOfWeek(events, ToLocal))
            .Add(UserSegments(events))
            .Add(Performance(events))
            .Add(Conversion(events))
            .Add(Feedback(events));

        if (context.Mode == ReportRunMode.Export)
        {
            document.Add(Sessions(events));
        }

        return document;
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

        return ReportSection.ForMetrics(S["Overview"].Value,
        [
            new ReportMetric(S["Sessions"].Value, ReportFormat.Number(events.Count)),
            new ReportMetric(S["Unique visitors"].Value, ReportFormat.Number(visitors.Count)),
            new ReportMetric(S["Resolved"].Value, ReportFormat.Number(resolved)),
            new ReportMetric(S["Abandoned"].Value, ReportFormat.Number(abandoned), S["Ended without being resolved."].Value),
            new ReportMetric(S["Active"].Value, ReportFormat.Number(events.Count(evt => !evt.SessionEndedUtc.HasValue))),
            new ReportMetric(S["Containment rate"].Value, ReportFormat.Percent((double)resolved / events.Count)),
            new ReportMetric(S["Abandonment rate"].Value, ReportFormat.Percent((double)abandoned / events.Count)),
            new ReportMetric(S["Average handle time"].Value, withHandleTime.Count > 0 ? ReportFormat.Duration(withHandleTime.Average(evt => evt.HandleTimeSeconds)) : "-"),
            new ReportMetric(S["Messages per session"].Value, Decimal(events.Average(evt => evt.MessageCount))),
            new ReportMetric(S["Returning visitors"].Value, visitors.Count > 0 ? ReportFormat.Percent((double)visitors.Count(group => group.Count() > 1) / visitors.Count) : "-", S["Visitors with more than one session."].Value),
            new ReportMetric(S["Messages to resolution"].Value, resolvedEvents.Count > 0 ? Decimal(resolvedEvents.Average(evt => evt.MessageCount)) : "-"),
        ]);
    }

    private ReportSection TimeOfDay(IReadOnlyList<AIChatSessionEvent> events, Func<DateTime, DateTime> toLocal)
    {
        var counts = new double[24];

        foreach (var evt in events)
        {
            counts[toLocal(evt.SessionStartedUtc).Hour]++;
        }

        return ReportSection.ForChart(S["Sessions by time of day"].Value, new ReportChart
        {
            Type = ReportChartType.Bar,
            ShowLegend = false,
            Labels = Enumerable.Range(0, 24).Select(hour => new DateTime(2000, 1, 1, hour, 0, 0).ToString("h tt", CultureInfo.CurrentCulture)).ToList(),
            Datasets = [new ReportChartDataset(S["Sessions"].Value, counts)],
        });
    }

    private ReportSection DayOfWeek(IReadOnlyList<AIChatSessionEvent> events, Func<DateTime, DateTime> toLocal)
    {
        var counts = new double[7];

        foreach (var evt in events)
        {
            counts[(int)toLocal(evt.SessionStartedUtc).DayOfWeek]++;
        }

        return ReportSection.ForChart(S["Sessions by day of week"].Value, new ReportChart
        {
            Type = ReportChartType.Bar,
            ShowLegend = false,
            Labels = CultureInfo.CurrentCulture.DateTimeFormat.DayNames.ToList(),
            Datasets = [new ReportChartDataset(S["Sessions"].Value, counts)],
        });
    }

    private ReportSection UserSegments(IReadOnlyList<AIChatSessionEvent> events)
    {
        var authenticated = events.Count(evt => evt.IsAuthenticated);

        return ReportSection.ForMetrics(S["Who chats"].Value,
        [
            new ReportMetric(S["Authenticated sessions"].Value, ReportFormat.Number(authenticated), ReportFormat.Percent((double)authenticated / events.Count)),
            new ReportMetric(S["Anonymous sessions"].Value, ReportFormat.Number(events.Count - authenticated), ReportFormat.Percent((double)(events.Count - authenticated) / events.Count)),
            new ReportMetric(S["Authenticated users"].Value, ReportFormat.Number(events.Where(evt => evt.IsAuthenticated && !string.IsNullOrEmpty(evt.UserId)).Select(evt => evt.UserId).Distinct(StringComparer.Ordinal).Count())),
            new ReportMetric(S["Anonymous visitors"].Value, ReportFormat.Number(events.Where(evt => !evt.IsAuthenticated && !string.IsNullOrEmpty(evt.VisitorId)).Select(evt => evt.VisitorId).Distinct(StringComparer.Ordinal).Count())),
        ]);
    }

    private ReportSection Performance(IReadOnlyList<AIChatSessionEvent> events)
    {
        var withLatency = events.Where(evt => evt.AverageResponseLatencyMs > 0).ToList();
        var withTokens = events.Where(evt => evt.TotalInputTokens > 0 || evt.TotalOutputTokens > 0).ToList();
        var input = withTokens.Sum(evt => (long)evt.TotalInputTokens);
        var output = withTokens.Sum(evt => (long)evt.TotalOutputTokens);

        return ReportSection.ForMetrics(S["Performance"].Value,
        [
            new ReportMetric(S["Average response latency"].Value, withLatency.Count > 0 ? S["{0} ms", Math.Round(withLatency.Average(evt => evt.AverageResponseLatencyMs)).ToString("N0", CultureInfo.CurrentCulture)].Value : "-", S["{0} sessions measured", withLatency.Count].Value),
            new ReportMetric(S["Input tokens"].Value, ReportFormat.Number(input)),
            new ReportMetric(S["Output tokens"].Value, ReportFormat.Number(output)),
            new ReportMetric(S["Total tokens"].Value, ReportFormat.Number(input + output)),
            new ReportMetric(S["Tokens per session"].Value, withTokens.Count > 0 ? ReportFormat.Number((long)Math.Round((double)(input + output) / withTokens.Count)) : "-", S["{0} sessions measured", withTokens.Count].Value),
        ]);
    }

    private ReportSection Conversion(IReadOnlyList<AIChatSessionEvent> events)
    {
        var closed = events.Where(evt => evt.SessionEndedUtc.HasValue).ToList();
        var scored = events
            .Where(evt => evt.ConversionScore.HasValue && evt.ConversionMaxScore.HasValue && evt.ConversionMaxScore.Value > 0)
            .ToList();
        var metrics = new List<ReportMetric>
        {
            new(S["AI resolved"].Value, ReportFormat.Number(closed.Count(evt => evt.IsResolved))),
            new(S["AI unresolved"].Value, ReportFormat.Number(closed.Count(evt => !evt.IsResolved))),
            new(S["AI resolution rate"].Value, closed.Count > 0 ? ReportFormat.Percent((double)closed.Count(evt => evt.IsResolved) / closed.Count) : "-", S["Of the sessions that ended."].Value),
        };

        if (scored.Count > 0)
        {
            var score = scored.Sum(evt => evt.ConversionScore!.Value);
            var maxScore = scored.Sum(evt => evt.ConversionMaxScore!.Value);
            var high = scored.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value >= 0.7);
            var low = scored.Count(evt => (double)evt.ConversionScore!.Value / evt.ConversionMaxScore!.Value < 0.3);

            metrics.Add(new ReportMetric(S["Sessions scored"].Value, ReportFormat.Number(scored.Count)));
            metrics.Add(new ReportMetric(S["Average conversion score"].Value, ReportFormat.Percent((double)score / maxScore), S["{0} of {1} points", score, maxScore].Value));
            metrics.Add(new ReportMetric(S["High performing"].Value, ReportFormat.Number(high), S["70% of the points or more; {0}", ReportFormat.Percent((double)high / scored.Count)].Value));
            metrics.Add(new ReportMetric(S["Low performing"].Value, ReportFormat.Number(low), S["Under 30% of the points; {0}", ReportFormat.Percent((double)low / scored.Count)].Value));
        }

        return ReportSection.ForMetrics(S["Resolution and conversion"].Value, metrics);
    }

    private ReportSection Feedback(IReadOnlyList<AIChatSessionEvent> events)
    {
        var up = events.Sum(evt => evt.ThumbsUpCount);
        var down = events.Sum(evt => evt.ThumbsDownCount);
        var ratings = up + down;
        var withFeedback = events.Count(evt => evt.ThumbsUpCount > 0 || evt.ThumbsDownCount > 0);

        return ReportSection.ForMetrics(S["Feedback"].Value,
        [
            new ReportMetric(S["Thumbs up"].Value, ReportFormat.Number(up), ratings > 0 ? ReportFormat.Percent((double)up / ratings) : null),
            new ReportMetric(S["Thumbs down"].Value, ReportFormat.Number(down), ratings > 0 ? ReportFormat.Percent((double)down / ratings) : null),
            new ReportMetric(S["Sessions without feedback"].Value, ReportFormat.Number(events.Count - withFeedback)),
            new ReportMetric(S["Feedback rate"].Value, ReportFormat.Percent((double)withFeedback / events.Count), S["Sessions with at least one rating."].Value),
        ]);
    }

    private ReportSection Sessions(IReadOnlyList<AIChatSessionEvent> events)
        => ReportSection.ForTable(S["Sessions"].Value,
        [
            new ReportColumn("SessionId"),
            new ReportColumn("ProfileId"),
            new ReportColumn("VisitorId"),
            new ReportColumn("UserId"),
            new ReportColumn("IsAuthenticated"),
            new ReportColumn("SessionStartedUtc"),
            new ReportColumn("SessionEndedUtc"),
            new ReportColumn("MessageCount", ReportColumnAlign.End),
            new ReportColumn("HandleTimeSeconds", ReportColumnAlign.End),
            new ReportColumn("IsResolved"),
        ],
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

    private static string Decimal(double value)
        => value.ToString("0.0", CultureInfo.InvariantCulture);
}
