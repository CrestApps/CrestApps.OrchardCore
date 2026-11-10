using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

/// <summary>
/// The QA dashboard as a report: it reads the period of the date range, narrows to a QA cohort's label when one is
/// chosen, shows the figures, links each lowest-scored session to its conversation with its AI profile, and starts on
/// the last 7 days.
/// </summary>
public sealed class AIQualityReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAIQADashboardService> _dashboardService = new();
    private readonly Mock<IAIQACohortStore> _cohortStore = new();
    private readonly Mock<IAIProfileStore> _profileStore = new();
    private readonly Mock<LinkGenerator> _linkGenerator = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    public AIQualityReportTests()
    {
        _httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        // The conversation page of the AI chat area: ai/chat/session/{profileId}/{sessionId}.
        _linkGenerator
            .Setup(generator => generator.GetPathByAddress(
                It.IsAny<HttpContext>(),
                It.IsAny<RouteValuesAddress>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(),
                It.IsAny<LinkOptions>()))
            .Returns((HttpContext _, RouteValuesAddress _, RouteValueDictionary values, RouteValueDictionary _, PathString? _, FragmentString _, LinkOptions _) =>
                values["area"] as string == "CrestApps.OrchardCore.AI.Chat" && values["controller"] as string == "Admin" && values["action"] as string == "Index"
                    ? $"/Admin/ai/chat/session/{values["profileId"]}/{values["sessionId"]}"
                    : null);

        _profileStore
            .Setup(store => store.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIProfile { ItemId = "profile-1", Name = "support", DisplayText = "Support assistant" });
    }

    [Fact]
    public async Task WhenNothingWasScored_ThePageSaysWhenConversationsAppear_AndTheExportIsEmpty()
    {
        _dashboardService
            .Setup(service => service.BuildAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIQADashboard());

        var document = await RunAsync(Filter(cohortId: null));
        var export = await RunAsync(Filter(cohortId: null), ReportRunMode.Export);

        var section = Assert.Single(document.Sections);
        Assert.Equal("Nothing scored yet", section.Title);
        Assert.StartsWith("Conversations appear here once a QA cohort", section.EmptyMessage);
        Assert.False(export.HasContent);
    }

    [Fact]
    public async Task WithScoredConversations_TheFiguresAreShown_ForThePeriod()
    {
        _dashboardService
            .Setup(service => service.BuildAsync(null, _day, _day.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dashboard());

        var document = await RunAsync(Filter(cohortId: null));

        var summary = document.Sections.Single(section => section.Title == "Summary").Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);
        Assert.Equal("2", summary["Scored"]);
        Assert.Equal("65", summary["Average score"]);
        Assert.Equal(AIConversationIssueKinds.Hallucination, Assert.Single(document.Sections.Single(section => section.Title == "Issues found").Bars).Label);
        Assert.Equal(["Score", "Passed", "Evaluated", "Session", "AI profile", "Summary"], LowestScores(document).Columns.Select(column => column.Label));
    }

    [Fact]
    public async Task EachLowestScoredSession_OpensItsConversation_AndNamesItsProfile()
    {
        _dashboardService
            .Setup(service => service.BuildAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dashboard());

        var document = await RunAsync(Filter(cohortId: null));

        var worst = LowestScores(document).Rows[0];
        Assert.True(ReportValue.TryGetLink(worst.Cells[3], out var text, out var url));
        Assert.Equal("b", text);
        Assert.Equal("/Admin/ai/chat/session/profile-1/b", url);
        Assert.Equal("Support assistant", worst.Cells[4]);

        // The evaluated time is shown in the site time zone.
        Assert.Equal("2026-11-01 21:00", worst.Cells[2]);

        // A session without a profile can't be opened, so it is plain text, and its profile is left empty.
        var orphan = LowestScores(document).Rows[1];
        Assert.Equal("a", orphan.Cells[3]);
        Assert.Null(orphan.Cells[4]);
    }

    [Fact]
    public async Task TheExport_WritesTheSession_WithoutALink()
    {
        _dashboardService
            .Setup(service => service.BuildAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dashboard());

        var document = await RunAsync(Filter(cohortId: null), ReportRunMode.Export);

        Assert.Equal("b", LowestScores(document).Rows[0].Cells[3]);
        Assert.Equal("Support assistant", LowestScores(document).Rows[0].Cells[4]);
    }

    [Fact]
    public async Task AChosenCohort_NarrowsTheFiguresToItsLabel()
    {
        var cohort = new AIQACohort { ItemId = "cohort-1", Name = "Support" };

        _cohortStore.Setup(store => store.FindByIdAsync("cohort-1", It.IsAny<CancellationToken>())).ReturnsAsync(cohort);
        _dashboardService
            .Setup(service => service.BuildAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIQADashboard());

        await RunAsync(Filter("cohort-1"));

        _dashboardService.Verify(service => service.BuildAsync(cohort.GetLabel(), _day, _day.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AnUnknownCohort_ShowsNothing()
    {
        var document = await RunAsync(Filter("missing"));

        Assert.False(document.HasContent);
        _dashboardService.VerifyNoOtherCalls();
    }

    [Fact]
    public void TheReport_KeepsTheQualityPermission_AndStartsOnTheLast7Days()
    {
        var descriptor = ReportDescriptor.Create<AIQualityReport>();

        Assert.Equal(AIPermissions.ManageAIQuality.Name, descriptor.Permission.Name);
        Assert.Null(descriptor.ExportPermission);
        Assert.Equal(ReportDateRangePeriod.LastDays, descriptor.DefaultDateRange.Period);
        Assert.Equal(7, descriptor.DefaultDateRange.Days);
        Assert.Equal("last7", descriptor.DefaultDateRange.Key);
    }

    [Fact]
    public void TheDashboardButtonOfACohort_OpensItsLast7Days()
    {
        // Thursday 5 November 2026, 15:30 in the site's time zone.
        var values = AIQualityReport.GetDashboardRouteValues("cohort-1", new DateTime(2026, 11, 5, 15, 30, 0));

        Assert.Equal(ReportsConstants.Feature, values["area"]);
        Assert.Equal(AIQualityReport.Name, values["id"]);
        Assert.Equal("cohort-1", values["QACohortId"]);
        Assert.Equal("2026-10-30T00:00", values["From"]);
        Assert.Equal("2026-11-05T23:59", values["To"]);
        Assert.Equal("last7", values["Range"]);
        Assert.False(AIQualityReport.GetDashboardRouteValues(null, new DateTime(2026, 11, 5)).ContainsKey("QACohortId"));
    }

    private Task<ReportDocument> RunAsync(ReportFilter filter, ReportRunMode mode = ReportRunMode.Display)
        => new AIQualityReport(
            _dashboardService.Object,
            _cohortStore.Object,
            _profileStore.Object,
            _linkGenerator.Object,
            _httpContextAccessor.Object,
            ReportTestClocks.LocalClock(TimeSpan.FromHours(-5)),
            new PassThroughStringLocalizer<AIQualityReport>())
            .RunAsync(new ReportContext(filter, mode), TestContext.Current.CancellationToken);

    private static AIQADashboard Dashboard()
        => AIQADashboardService.Summarize(
        [
            new() { SessionId = "a", Score = 90, Passed = true, Resolved = true, Sentiment = "Positive", EvaluatedUtc = _day.AddHours(1) },
            new() { SessionId = "b", ProfileId = "profile-1", Score = 40, Passed = false, Resolved = false, Sentiment = "Negative", EvaluatedUtc = _day.AddHours(2), Issues = [new() { Kind = AIConversationIssueKinds.Hallucination }] },
        ]);

    private static ReportSection LowestScores(ReportDocument document)
        => document.Sections.Single(section => section.Title == "Lowest scores");

    private static ReportFilter Filter(string cohortId)
    {
        var filter = new ReportFilter { ReportName = AIQualityReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });
        filter.Set(AIQualityReport.CohortIdFilterKey, cohortId);

        return filter;
    }
}
