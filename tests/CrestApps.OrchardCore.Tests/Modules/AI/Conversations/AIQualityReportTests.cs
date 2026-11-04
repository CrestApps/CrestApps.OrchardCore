using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

/// <summary>
/// The QA dashboard as a report: it reads the period of the date range, narrows to a QA cohort's label when one is
/// chosen, and shows the figures, or nothing when nothing was scored.
/// </summary>
public sealed class AIQualityReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAIQADashboardService> _dashboardService = new();
    private readonly Mock<IAIQACohortStore> _cohortStore = new();

    [Fact]
    public async Task WhenNothingWasScored_TheDocumentIsEmpty()
    {
        _dashboardService
            .Setup(service => service.BuildAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIQADashboard());

        var document = await RunAsync(Filter(cohortId: null));

        Assert.False(document.HasContent);
    }

    [Fact]
    public async Task WithScoredConversations_TheFiguresAreShown_ForThePeriod()
    {
        var dashboard = AIQADashboardService.Summarize(
        [
            new() { SessionId = "a", Score = 90, Passed = true, Resolved = true, Sentiment = "Positive", EvaluatedUtc = _day.AddHours(1) },
            new() { SessionId = "b", Score = 40, Passed = false, Resolved = false, Sentiment = "Negative", EvaluatedUtc = _day.AddHours(2), Issues = [new() { Kind = AIConversationIssueKinds.Hallucination }] },
        ]);

        _dashboardService
            .Setup(service => service.BuildAsync(null, _day, _day.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var document = await RunAsync(Filter(cohortId: null));

        var summary = document.Sections.Single(section => section.Title == "Summary").Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);
        Assert.Equal("2", summary["Scored"]);
        Assert.Equal("65", summary["Average score"]);
        Assert.Equal(AIConversationIssueKinds.Hallucination, Assert.Single(document.Sections.Single(section => section.Title == "Issues found").Bars).Label);
        Assert.Equal("b", document.Sections.Single(section => section.Title == "Lowest scores").Rows[0].Cells[3]);
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
    public void TheReport_KeepsTheQualityPermission()
        => Assert.Equal(AIPermissions.ManageAIQuality.Name, AIQualityReport.Permission.Name);

    private Task<ReportDocument> RunAsync(ReportFilter filter)
        => new AIQualityReport(_dashboardService.Object, _cohortStore.Object, new PassThroughStringLocalizer<AIQualityReport>())
            .RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);

    private static ReportFilter Filter(string cohortId)
    {
        var filter = new ReportFilter { ReportName = AIQualityReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });
        filter.Set(AIQualityReport.CohortIdFilterKey, cohortId);

        return filter;
    }
}
