using System.Globalization;
using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
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
    public async Task TheExport_IsOneFlatTable_WithTheColumnsAndValuesOfTheOldCsv()
    {
        var session = Session("s1", "profile-1", "visitor-1", hour: 9, resolved: true, messages: 4);
        session.UserId = "user-1";
        session.IsAuthenticated = true;
        session.HandleTimeSeconds = 300;
        _reader.Events.Add(session);

        var document = await RunAsync(Filter(), ReportRunMode.Export);

        Assert.Single(document.Sections);
        Assert.Equal(
        [
            "SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved",
            "s1,profile-1,visitor-1,user-1,True,2026-11-02T09:00:00.0000000Z,2026-11-02T09:05:00.0000000Z,4,300,True",
        ], CsvLines(document));
    }

    [Fact]
    public async Task TheExport_WithoutSessions_KeepsItsHeader()
    {
        var document = await RunAsync(Filter(), ReportRunMode.Export);

        Assert.Equal(["SessionId,ProfileId,VisitorId,UserId,IsAuthenticated,SessionStartedUtc,SessionEndedUtc,MessageCount,HandleTimeSeconds,IsResolved"], CsvLines(document));
    }

    [Fact]
    public async Task Performance_ShowsTheAverageInputAndOutputTokensPerSession()
    {
        var first = Session("s1", "profile-1", "visitor-1", hour: 9);
        first.TotalInputTokens = 100;
        first.TotalOutputTokens = 40;
        var second = Session("s2", "profile-1", "visitor-2", hour: 10);
        second.TotalInputTokens = 302;
        second.TotalOutputTokens = 20;
        var unmeasured = Session("s3", "profile-1", "visitor-3", hour: 11);
        _reader.Events.AddRange([first, second, unmeasured]);

        var document = await RunAsync(Filter());

        var performance = Metrics(document, "Performance");
        Assert.Equal("462", performance["Total tokens"]);
        Assert.Equal("231", performance["Tokens per session"]);
        Assert.Equal("201", performance["Input tokens per session"]);
        Assert.Equal("30", performance["Output tokens per session"]);
    }

    [Fact]
    public async Task EachSection_HasItsGuidance_EachFigureItsExplanation_AndAnEmptySectionSaysHowToCollectIt()
    {
        _reader.Events.Add(Session("s1", "profile-1", "visitor-1", hour: 9, ended: false));

        var document = await RunAsync(Filter());

        Assert.All(document.Sections, section => Assert.False(string.IsNullOrEmpty(section.Description)));
        Assert.All(document.Sections.SelectMany(section => section.Metrics), metric => Assert.False(string.IsNullOrEmpty(metric.Description)));

        // No tokens, no ended session and no rating: the sections say how to start collecting them.
        var performance = document.Sections.Single(section => section.Title == "Performance");
        Assert.Empty(performance.Metrics);
        Assert.StartsWith("No performance data available.", performance.EmptyMessage);
        var conversion = document.Sections.Single(section => section.Title == "Resolution and conversion");
        Assert.Empty(conversion.Metrics);
        Assert.Contains("Enable Resolution Detection", conversion.EmptyMessage);
        Assert.Empty(document.Sections.Single(section => section.Title == "Feedback").Metrics);
    }

    [Fact]
    public async Task TheHours_ReadAsA24HourClock_InACultureWithoutAMOrPM()
    {
        _reader.Events.Add(Session("s1", "profile-1", "visitor-1", hour: 13));

        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.DateTimeFormat.AMDesignator = string.Empty;
        culture.DateTimeFormat.PMDesignator = string.Empty;

        var labels = await TimeOfDayLabelsAsync(culture);

        Assert.Equal(24, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("00:00", labels[0]);
        Assert.Equal("13:00", labels[13]);
    }

    [Fact]
    public async Task TheHours_KeepAMAndPM_InACultureThatHasThem()
    {
        _reader.Events.Add(Session("s1", "profile-1", "visitor-1", hour: 13));

        var labels = await TimeOfDayLabelsAsync(CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(24, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("12 AM", labels[0]);
        Assert.Equal("1 PM", labels[13]);
    }

    [Fact]
    public async Task AnOpenEndedRange_ReachesTheReader_AsPicked()
    {
        _reader.Events.Add(Session("s1", "profile-1", "visitor-1", hour: 9));

        var filter = new ReportFilter { ReportName = AIChatSessionAnalyticsReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, Key = "after" });

        await RunAsync(filter);

        Assert.Equal(_day, _reader.LastQuery.FromUtc);
        Assert.Null(_reader.LastQuery.ToUtc);
    }

    [Fact]
    public void TheReport_StartsOnAllTime_AndExportsWithTheExportPermission()
    {
        var descriptor = ReportDescriptor.Create<AIChatSessionAnalyticsReport>();

        Assert.Equal(ReportDateRangePeriod.AllTime, descriptor.DefaultDateRange.Period);
        Assert.Equal("ExportChatAnalytics", descriptor.ExportPermission.Name);
        Assert.Equal("ViewChatAnalytics", descriptor.Permission.Name);
    }

    [Fact]
    public void TheReport_UsesTheChatAnalyticsPermission_AndTheProfileFilter()
    {
        Assert.Equal("ViewChatAnalytics", AIChatSessionAnalyticsReport.Permission.Name);
        Assert.Equal(ReportsConstants.Categories.AIReports, AIChatSessionAnalyticsReport.Category);
        Assert.Contains(AIChatReportFilters.Profile, Report().FilterNames);
    }

    private async Task<IReadOnlyList<string>> TimeOfDayLabelsAsync(CultureInfo culture)
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = culture;

            var document = await RunAsync(Filter());

            return document.Sections.Single(section => section.Title == "Sessions by time of day").Chart.Labels.ToList();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // The CSV export, as scripts read it: its lines, without the byte order mark.
    internal static IReadOnlyList<string> CsvLines(ReportDocument document)
        => Encoding.UTF8.GetString(new CsvReportExportFormat().Serialize(document))
            .TrimStart('\uFEFF')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

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
