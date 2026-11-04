using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The AI chat session analytics report, on the Reports framework: the overview, the time charts and the other
/// figures come from the sessions of the period, and the profile filter narrows them.
/// </summary>
public sealed class AIChatSessionAnalyticsReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryAIChatSessionEventReader _reader = new();

    [Fact]
    public async Task WithoutSessions_TheDocumentIsEmpty()
    {
        var document = await RunAsync(Filter());

        Assert.False(document.HasContent);
    }

    [Fact]
    public async Task WithSessions_TheOverviewAndTheTimeChartsCountThem()
    {
        _reader.Events.AddRange(
        [
            Session("s1", "profile-1", "visitor-1", hour: 9, resolved: true, ended: true, messages: 4, thumbsUp: 1),
            Session("s2", "profile-1", "visitor-1", hour: 9, resolved: false, ended: true, messages: 2),
            Session("s3", "profile-2", "visitor-2", hour: 14, resolved: false, ended: false, messages: 6, thumbsDown: 1),
        ]);

        var document = await RunAsync(Filter());

        var overview = Metrics(document, "Overview");
        Assert.Equal("3", overview["Sessions"]);
        Assert.Equal("2", overview["Unique visitors"]);
        Assert.Equal("1", overview["Resolved"]);
        Assert.Equal("1", overview["Abandoned"]);
        Assert.Equal("1", overview["Active"]);
        Assert.Equal("4.0", overview["Messages per session"]);

        var timeOfDay = document.Sections.Single(section => section.Title == "Sessions by time of day").Chart;
        Assert.Equal(24, timeOfDay.Labels.Count);
        Assert.Equal(2, timeOfDay.Datasets[0].Values[9]);
        Assert.Equal(1, timeOfDay.Datasets[0].Values[14]);

        var dayOfWeek = document.Sections.Single(section => section.Title == "Sessions by day of week").Chart;
        Assert.Equal(3, dayOfWeek.Datasets[0].Values[(int)_day.DayOfWeek]);

        var feedback = Metrics(document, "Feedback");
        Assert.Equal("1", feedback["Thumbs up"]);
        Assert.Equal("1", feedback["Thumbs down"]);

        // On screen there is no row per session; the export adds it.
        Assert.DoesNotContain(document.Sections, section => section.Title == "Sessions");
    }

    [Fact]
    public async Task TheProfileAndDateRange_ReachTheQuery_AndNarrowTheSessions()
    {
        _reader.Events.AddRange(
        [
            Session("s1", "profile-1", "visitor-1", hour: 9),
            Session("s2", "profile-2", "visitor-2", hour: 10),
        ]);

        var filter = Filter();
        filter.Set(AIChatReportFilters.ProfileId, "profile-1");

        var document = await RunAsync(filter);

        Assert.Equal("profile-1", _reader.LastQuery.ProfileId);
        Assert.Equal(_day, _reader.LastQuery.FromUtc);
        Assert.Equal(_day.AddDays(1).AddTicks(-1), _reader.LastQuery.ToUtc);
        Assert.Equal("1", Metrics(document, "Overview")["Sessions"]);
    }

    [Fact]
    public async Task TheExport_AddsARowPerSession()
    {
        _reader.Events.Add(Session("s1", "profile-1", "visitor-1", hour: 9));

        var document = await RunAsync(Filter(), ReportRunMode.Export);

        var sessions = document.Sections.Single(section => section.Title == "Sessions");
        Assert.Equal("s1", Assert.Single(sessions.Rows).Cells[0]);
    }

    [Fact]
    public void TheReport_UsesTheChatAnalyticsPermission_AndTheProfileFilter()
    {
        Assert.Equal("ViewChatAnalytics", AIChatSessionAnalyticsReport.Permission.Name);
        Assert.Equal(ReportsConstants.Categories.AIReports, AIChatSessionAnalyticsReport.Category);
        Assert.Contains(AIChatReportFilters.Profile, Report().FilterNames);
    }

    private Task<ReportDocument> RunAsync(ReportFilter filter, ReportRunMode mode = ReportRunMode.Display)
        => Report().RunAsync(new ReportContext(filter, mode), TestContext.Current.CancellationToken);

    private AIChatSessionAnalyticsReport Report()
        => new(_reader, ReportTestClocks.LocalClock(), ReportTestClocks.Clock(), new PassThroughStringLocalizer<AIChatSessionAnalyticsReport>());

    private static ReportFilter Filter()
    {
        var filter = new ReportFilter { ReportName = AIChatSessionAnalyticsReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });

        return filter;
    }

    private static Dictionary<string, string> Metrics(ReportDocument document, string title)
        => document.Sections.Single(section => section.Title == title).Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);

    private static AIChatSessionEvent Session(
        string sessionId,
        string profileId,
        string visitorId,
        int hour,
        bool resolved = false,
        bool ended = true,
        int messages = 2,
        int thumbsUp = 0,
        int thumbsDown = 0)
        => new()
        {
            SessionId = sessionId,
            ProfileId = profileId,
            VisitorId = visitorId,
            SessionStartedUtc = _day.AddHours(hour),
            SessionEndedUtc = ended ? _day.AddHours(hour).AddMinutes(5) : null,
            IsResolved = resolved,
            MessageCount = messages,
            ThumbsUpCount = thumbsUp,
            ThumbsDownCount = thumbsDown,
        };
}
