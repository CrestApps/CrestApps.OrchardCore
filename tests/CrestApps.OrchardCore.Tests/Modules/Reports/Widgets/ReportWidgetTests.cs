using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using CrestApps.OrchardCore.Reports.Widgets.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Widgets;

/// <summary>
/// A report dashboard widget covers a period that moves with the clock, shows what fits a tile, and keeps its result so
/// the dashboard doesn't run the report on every visit.
/// </summary>
public sealed class ReportWidgetTests
{
    private static readonly DateTime _localNow = new(2026, 10, 23, 15, 30, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(ReportWidgetPeriod.Today, "2026-10-23", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.Yesterday, "2026-10-22", "2026-10-22")]
    [InlineData(ReportWidgetPeriod.Last7Days, "2026-10-17", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.Last30Days, "2026-09-24", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.Last90Days, "2026-07-26", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.ThisMonth, "2026-10-01", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.LastMonth, "2026-09-01", "2026-09-30")]
    [InlineData(ReportWidgetPeriod.ThisYear, "2026-01-01", "2026-10-23")]
    [InlineData(ReportWidgetPeriod.Last12Months, "2025-11-01", "2026-10-23")]
    public void ARollingPeriod_RunsFromItsFirstDayToItsLast(ReportWidgetPeriod period, string first, string last)
    {
        // Act
        var (from, to) = ReportWidgetPeriods.Resolve(period, _localNow);

        // Assert
        Assert.Equal(DateTime.Parse(first), from);
        Assert.Equal(DateTime.Parse(last).AddDays(1).AddTicks(-1), to);
    }

    [Fact]
    public void AllTime_HasNoBounds()
    {
        // Act
        var (from, to) = ReportWidgetPeriods.Resolve(ReportWidgetPeriod.AllTime, _localNow);

        // Assert
        Assert.Null(from);
        Assert.Null(to);
    }

    [Fact]
    public void ACustomPeriod_CoversTheWholeOfItsLastDay()
    {
        // Act
        var (from, to) = ReportWidgetPeriods.Resolve(ReportWidgetPeriod.Custom, _localNow, new DateTime(2026, 3, 1, 9, 0, 0), new DateTime(2026, 3, 31, 9, 0, 0));

        // Assert
        Assert.Equal(new DateTime(2026, 3, 1), from);
        Assert.Equal(new DateTime(2026, 4, 1).AddTicks(-1), to);
    }

    [Fact]
    public void ACustomPeriodGivenBackwards_IsPutRightWayRound()
    {
        // Act
        var (from, to) = ReportWidgetPeriods.Resolve(ReportWidgetPeriod.Custom, _localNow, new DateTime(2026, 3, 31), new DateTime(2026, 3, 1));

        // Assert
        Assert.Equal(new DateTime(2026, 3, 1), from);
        Assert.Equal(new DateTime(2026, 4, 1).AddTicks(-1), to);
    }

    [Fact]
    public void ACustomPeriodWithOnlyAStart_IsOpenAtTheEnd()
    {
        // Act
        var (from, to) = ReportWidgetPeriods.Resolve(ReportWidgetPeriod.Custom, _localNow, new DateTime(2026, 3, 1), null);

        // Assert
        Assert.Equal(new DateTime(2026, 3, 1), from);
        Assert.Null(to);
    }

    [Fact]
    public void FiguresAndTables_DropsChartsAndKeepsTheFirstRowsAndTheTotal()
    {
        // Arrange
        var document = Document();

        // Act
        ReportWidgetTrimmer.Trim(document, ReportWidgetContent.FiguresAndTables, 2);

        // Assert
        Assert.Equal([ReportSectionKind.Metrics, ReportSectionKind.Bars, ReportSectionKind.Table], document.Sections.Select(section => section.Kind));

        var table = document.Sections.Single(section => section.Kind == ReportSectionKind.Table);

        Assert.Equal(["a", "b", "total"], table.Rows.Select(row => row.Cells[0]));
        Assert.Equal(2, document.Sections.Single(section => section.Kind == ReportSectionKind.Bars).Bars.Count);
    }

    [Theory]
    [InlineData(ReportWidgetContent.FiguresOnly, new[] { ReportSectionKind.Metrics, ReportSectionKind.Bars })]
    [InlineData(ReportWidgetContent.TablesOnly, new[] { ReportSectionKind.Table })]
    [InlineData(ReportWidgetContent.ChartsOnly, new[] { ReportSectionKind.Chart })]
    [InlineData(ReportWidgetContent.Everything, new[] { ReportSectionKind.Metrics, ReportSectionKind.Bars, ReportSectionKind.Table, ReportSectionKind.Chart })]
    public void TheContentChosen_DecidesWhichSectionsStay(ReportWidgetContent content, ReportSectionKind[] expected)
    {
        // Act
        var document = ReportWidgetTrimmer.Trim(Document(), content, 0);

        // Assert
        Assert.Equal(expected, document.Sections.Select(section => section.Kind));
    }

    [Fact]
    public async Task TheReportRunsOnce_WhileItsResultIsKept()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();

        // Act
        var first = await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);
        var second = await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, first.Outcome);
        Assert.Equal(ReportWidgetOutcome.Shown, second.Outcome);
        Assert.Equal(1, fixture.Runs);
    }

    [Fact]
    public async Task TheReportRunsEveryTime_WhenTheWidgetKeepsNothing()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.CacheMinutes = 0;

        // Act
        await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);
        await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, fixture.Runs);
    }

    [Fact]
    public async Task SavingTheWidget_StartsTheKeptResultOver()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();

        await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        part.ContentItem.ModifiedUtc = part.ContentItem.ModifiedUtc!.Value.AddMinutes(1);

        // Act
        await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, fixture.Runs);
    }

    [Fact]
    public async Task TheReportIsRunForThePeriodTheWidgetCovers()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.Last7Days;

        // Act
        await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        var range = fixture.LastFilter.GetDateRange();

        Assert.Equal(new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1), range.ToUtc);
    }

    [Fact]
    public async Task APersonWhoMayNotViewTheReport_IsShownNothingOfIt()
    {
        // Arrange
        var fixture = new Fixture { Allowed = false };

        // Act
        var result = await fixture.Service.GetAsync(Fixture.Part(), fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Denied, result.Outcome);
        Assert.Null(result.Document);
        Assert.Equal(0, fixture.Runs);
    }

    [Fact]
    public async Task AWidgetWithNoReport_SaysSo()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.ReportName = null;

        // Act
        var result = await fixture.Service.GetAsync(part, new ReportWidgetPartSettings(), fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.NotConfigured, result.Outcome);
    }

    [Fact]
    public async Task AReportThatFails_DoesNotTakeTheDashboardDown_AndIsNotKept()
    {
        // Arrange
        var fixture = new Fixture { Fails = true };
        var part = Fixture.Part();

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);
        fixture.Fails = false;
        var retry = await fixture.Service.GetAsync(part, fixture.Settings, fixture.User, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Failed, result.Outcome);
        Assert.Equal(ReportWidgetOutcome.Shown, retry.Outcome);
    }

    private static ReportDocument Document()
    {
        var document = new ReportDocument();

        document.Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Orders", "5")]));
        document.Add(ReportSection.ForBars("Top", [new ReportBar("x", "3", 1), new ReportBar("y", "2", .6), new ReportBar("z", "1", .3)]));
        document.Add(ReportSection.ForTable("Rows", [new ReportColumn("Name")],
        [
            new ReportRow(["a"]),
            new ReportRow(["b"]),
            new ReportRow(["c"]),
            new ReportRow(["total"], emphasize: true),
        ]));
        document.Add(ReportSection.ForChart("Trend", new ReportChart()));

        return document;
    }

    private sealed class Fixture
    {
        private static readonly DateTime _now = new(2026, 10, 23, 15, 30, 0, DateTimeKind.Utc);

        public Fixture()
        {
            var report = new Mock<IReportRunner>();

            report.Setup(value => value.RunAsync(It.IsAny<ReportContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReportContext context, CancellationToken _) =>
                {
                    Runs++;
                    LastFilter = context.Filter;

                    if (Fails)
                    {
                        throw new InvalidOperationException("The report broke.");
                    }

                    return Document();
                });

            var permission = new Permission("ViewTestReport");
            var descriptor = new ReportDescriptor(
                "test-report",
                LocalizationSource.Create<Fixture>("Test report"),
                null,
                "Commerce",
                permission,
                typeof(Fixture),
                _ => report.Object);

            var manager = new Mock<IReportManager>();

            manager.Setup(value => value.GetDescriptor("test-report")).Returns(descriptor);
            manager.Setup(value => value.GetReport("test-report")).Returns(report.Object);

            var authorization = new Mock<IAuthorizationService>();

            authorization.Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> _) => Allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var clock = new Mock<IClock>();

            clock.SetupGet(value => value.UtcNow).Returns(_now);

            var localClock = new Mock<ILocalClock>();

            localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
                .ReturnsAsync((DateTimeOffset value) => value);
            localClock.Setup(value => value.ConvertToUtcAsync(It.IsAny<DateTime>()))
                .ReturnsAsync((DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc));

            Service = new ReportWidgetService(
                manager.Object,
                authorization.Object,
                new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()),
                new SourceTextLocalizerFactory(),
                new MemoryCache(new MemoryCacheOptions()),
                localClock.Object,
                clock.Object,
                NullLogger<ReportWidgetService>.Instance);
        }

        public ReportWidgetService Service { get; }

        public ReportWidgetPartSettings Settings { get; } = new() { ReportName = "test-report" };

        public ClaimsPrincipal User { get; } = new(new ClaimsIdentity("test"));

        public bool Allowed { get; set; } = true;

        public bool Fails { get; set; }

        public int Runs { get; private set; }

        public ReportFilter LastFilter { get; private set; }

        public static ReportWidgetPart Part()
        {
            var item = new ContentItem { ContentItemId = "widget-1", ModifiedUtc = _now };
            var part = new ReportWidgetPart { ReportName = "test-report" };

            part.ContentItem = item;

            return part;
        }
    }
}
