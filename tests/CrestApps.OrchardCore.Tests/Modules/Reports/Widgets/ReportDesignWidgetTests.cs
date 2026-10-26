using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Handlers;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using CrestApps.OrchardCore.Reports.Widgets.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Widgets;

/// <summary>
/// A dashboard widget shows a report built in the report builder, or a view, for a period that moves with the clock, to
/// the people who may open it, and keeps its result so the dashboard doesn't run the report on every visit.
/// </summary>
public sealed class ReportDesignWidgetTests
{
    // The orders of the sales data were placed on 10 January, 5 and 20 February, and 1 and 14 March 2026.
    private static readonly DateTime _now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AReport_RunsWithItsOwnersAccess_AndShowsItsDocument()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.Service.GetAsync(Fixture.Part(), fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, result.Outcome);
        Assert.Equal("Sales", result.Title);
        Assert.Equal("sales", result.ItemId);
        Assert.False(result.IsView);
        Assert.Equal([ReportSectionKind.Metrics, ReportSectionKind.Table, ReportSectionKind.Chart], result.Document.Sections.Select(section => section.Kind));
        Assert.Equal("395", Assert.Single(result.Document.Sections[0].Metrics).Value);
        Assert.Equal("owner", fixture.Data.Queries[^1].Context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(_now, result.GeneratedUtc);
    }

    [Fact]
    public async Task ThePeriod_IsSetOnTheReportsDateRangeFilter()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.Last30Days;
        part.MaxRows = 0;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.PeriodApplied);
        Assert.Equal(new DateTime(2026, 2, 14), result.From);
        Assert.Equal(new DateTime(2026, 3, 16).AddTicks(-1), result.To);
        Assert.Equal(["o3", "o4", "o5"], OrderIds(result.Document));
    }

    [Theory]
    [InlineData(ReportWidgetPeriod.ThisMonth, new[] { "o4", "o5" })]
    [InlineData(ReportWidgetPeriod.LastMonth, new[] { "o2", "o3" })]
    [InlineData(ReportWidgetPeriod.AllTime, new[] { "o1", "o2", "o3", "o4", "o5" })]
    public async Task EveryPeriod_SetsItsDaysOnTheFilter(ReportWidgetPeriod period, string[] expected)
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = period;
        part.MaxRows = 0;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, OrderIds(result.Document));
    }

    [Fact]
    public async Task ACustomPeriod_CoversItsWholeLastDay()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.Custom;
        part.From = new DateTime(2026, 2, 5);
        part.To = new DateTime(2026, 2, 20);

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["o2", "o3"], OrderIds(result.Document));
    }

    [Fact]
    public async Task ACustomPeriodWithOnlyAStart_IsOpenAtTheEnd()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.Custom;
        part.From = new DateTime(2026, 2, 21);
        part.To = null;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["o4", "o5"], OrderIds(result.Document));
    }

    [Fact]
    public async Task ThePeriod_SkipsAFilterThatIsNotADateRange()
    {
        // Arrange
        var design = Sales();
        design.Query.Filters.Insert(0, new ReportFilterDefinition
        {
            Id = "total",
            Field = "o.Total",
            Operator = DataFilterOperator.Between,
            Values = ["0", "1000"],
            Exposed = true,
        });

        var fixture = new Fixture(design);
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.ThisMonth;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.PeriodApplied);
        Assert.Equal(["o4", "o5"], OrderIds(result.Document));
    }

    [Theory]
    [InlineData(ReportWidgetPeriod.LastMonth, new[] { "o2", "o3" })]
    [InlineData(ReportWidgetPeriod.Custom, new[] { "o1" })]
    [InlineData(ReportWidgetPeriod.AllTime, new[] { "o1", "o2", "o3", "o4", "o5" })]
    public async Task ARelativeDateFilter_CoversTheWidgetsPeriodInstead(ReportWidgetPeriod period, string[] expected)
    {
        // Arrange
        var design = Sales();
        design.Query.Filters = [LastThirtyDays()];

        var fixture = new Fixture(design);
        var part = Fixture.Part();
        part.Period = period;
        part.From = new DateTime(2026, 1, 1);
        part.To = new DateTime(2026, 1, 31);
        part.MaxRows = 0;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.PeriodApplied);
        Assert.Equal(expected, OrderIds(result.Document));
    }

    [Fact]
    public async Task ARelativeDateFilter_CoversThePeriodForThatRunOnly()
    {
        // Arrange
        var design = Sales();
        design.Query.Filters = [LastThirtyDays()];

        var fixture = new Fixture(design);
        var february = new ReportRunPeriod(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        // Act
        var withPeriod = await fixture.Runner.RunAsync(design, filterValues: null, february, TestContext.Current.CancellationToken);
        var withoutPeriod = await fixture.Runner.RunAsync(design, filterValues: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("placed", withPeriod.PeriodFilterId);
        Assert.Equal(["o2", "o3"], OrderIds(withPeriod.Document));
        Assert.Null(withoutPeriod.PeriodFilterId);
        Assert.Equal(["o3", "o4", "o5"], OrderIds(withoutPeriod.Document));

        var saved = Assert.Single(design.Query.Filters);

        Assert.Equal(DataFilterOperator.InLastDays, saved.Operator);
        Assert.Equal(ReportFilterControl.RelativeDate, saved.Control);
        Assert.Equal(["30"], saved.Values);
    }

    [Fact]
    public async Task AReportWhoseDateFilterViewersCannotChange_KeepsItsOwnDates()
    {
        // Arrange
        var design = Sales();
        var fixedDays = LastThirtyDays();
        fixedDays.Exposed = false;
        design.Query.Filters =
        [
            fixedDays,
            new ReportFilterDefinition { Id = "order", Field = "o.Id", Operator = DataFilterOperator.In, Exposed = true, Control = ReportFilterControl.MultiSelect },
        ];

        var fixture = new Fixture(design);
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.LastMonth;
        part.MaxRows = 0;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, result.Outcome);
        Assert.False(result.PeriodApplied);
        Assert.Equal(["o3", "o4", "o5"], OrderIds(result.Document));
    }

    [Fact]
    public async Task AReportWithNoDateRangeFilter_KeepsItsOwnDates()
    {
        // Arrange
        var design = Sales();
        design.Query.Filters.Clear();

        var fixture = new Fixture(design);
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.Today;
        part.MaxRows = 0;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, result.Outcome);
        Assert.False(result.PeriodApplied);
        Assert.Equal(["o1", "o2", "o3", "o4", "o5"], OrderIds(result.Document));
    }

    [Theory]
    [InlineData(ReportWidgetContent.FiguresOnly, new[] { ReportSectionKind.Metrics })]
    [InlineData(ReportWidgetContent.TablesOnly, new[] { ReportSectionKind.Table })]
    [InlineData(ReportWidgetContent.ChartsOnly, new[] { ReportSectionKind.Chart })]
    [InlineData(ReportWidgetContent.FiguresAndTables, new[] { ReportSectionKind.Metrics, ReportSectionKind.Table })]
    [InlineData(ReportWidgetContent.Everything, new[] { ReportSectionKind.Metrics, ReportSectionKind.Table, ReportSectionKind.Chart })]
    public async Task WhatTheWidgetShows_KeepsThoseVisualsOfTheReport(ReportWidgetContent content, ReportSectionKind[] expected)
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Show = content;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.Document.Sections.Select(section => section.Kind));
    }

    [Fact]
    public async Task RowsPerTable_KeepsTheFirstRowsAndTheTotal()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.Period = ReportWidgetPeriod.AllTime;
        part.MaxRows = 2;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        var table = result.Document.Sections.Single(section => section.Kind == ReportSectionKind.Table);

        Assert.Equal(["o1", "o2"], OrderIds(result.Document));
        Assert.Equal(ReportRowKind.GrandTotal, table.Rows[^1].Kind);
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public async Task TheReportRunsOnce_WhileItsResultIsKept()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();

        // Act
        var first = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);
        var reads = fixture.Data.Queries.Count;
        var second = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, first.Outcome);
        Assert.Equal(ReportWidgetOutcome.Shown, second.Outcome);
        Assert.Same(first.Document, second.Document);
        Assert.Equal(reads, fixture.Data.Queries.Count);
    }

    [Fact]
    public async Task TheReportRunsEveryTime_WhenTheWidgetKeepsNothing()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.CacheMinutes = 0;

        // Act
        await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);
        var reads = fixture.Data.Queries.Count;
        await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(fixture.Data.Queries.Count > reads);
    }

    [Fact]
    public async Task PublishingTheReportAgain_StartsTheKeptResultOver()
    {
        // Arrange
        var design = Sales();
        var fixture = new Fixture(design);
        var part = Fixture.Part();

        await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        var reads = fixture.Data.Queries.Count;
        design.ModifiedUtc = _now.AddMinutes(5);

        // Act
        await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(fixture.Data.Queries.Count > reads);
    }

    [Fact]
    public async Task APersonTheReportIsNotSharedWith_IsShownNothingOfIt()
    {
        // Arrange
        var fixture = new Fixture();

        // Act
        var result = await fixture.Service.GetAsync(Fixture.Part(), fixture.Stranger, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Denied, result.Outcome);
        Assert.Null(result.Document);
        Assert.Null(result.Title);
        Assert.Empty(fixture.Data.Queries);
    }

    [Fact]
    public async Task TheKeptResult_IsNeverShownToAPersonWhoMayNotViewTheReport()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();

        await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Stranger, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Denied, result.Outcome);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task EveryoneWhoMayViewAReport_SharesItsKeptResult_BecauseItReadsWithItsOwnersAccess()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        var colleague = User("manager-2", "manager2", "Managers");

        var first = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);
        var reads = fixture.Data.Queries.Count;

        // Act
        var second = await fixture.Service.GetAsync(part, colleague, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, second.Outcome);
        Assert.Same(first.Document, second.Document);
        Assert.Equal(reads, fixture.Data.Queries.Count);
    }

    [Fact]
    public async Task AView_IsShownAsATable_ReadWithTheViewersAccess_AndKeptPerViewer()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.ReportId = null;
        part.ViewId = "orders-view";
        part.Show = ReportWidgetContent.ChartsOnly;
        part.Period = ReportWidgetPeriod.AllTime;

        // Act
        var first = await fixture.Service.GetAsync(part, fixture.Designer, TestContext.Current.CancellationToken);
        var firstReader = fixture.Data.Queries[^1].Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reads = fixture.Data.Queries.Count;
        var second = await fixture.Service.GetAsync(part, fixture.OtherDesigner, TestContext.Current.CancellationToken);
        var secondReader = fixture.Data.Queries[^1].Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Shown, first.Outcome);
        Assert.True(first.IsView);
        Assert.Equal("Orders", first.Title);
        Assert.Equal(ReportSectionKind.Table, Assert.Single(first.Document.Sections).Kind);
        Assert.Equal("designer-1", firstReader);
        Assert.Equal(ReportWidgetOutcome.Shown, second.Outcome);
        Assert.True(fixture.Data.Queries.Count > reads);
        Assert.Equal("designer-2", secondReader);
    }

    [Fact]
    public async Task AView_IsNotShownToAPersonWhoDoesNotDesignReports()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.ReportId = null;
        part.ViewId = "orders-view";

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Denied, result.Outcome);
        Assert.True(result.IsView);
        Assert.Empty(fixture.Data.Queries);
    }

    [Fact]
    public async Task AWidgetWithNoReport_SaysSo()
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.ReportId = null;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.NotConfigured, result.Outcome);
        Assert.False(result.IsMissing);
    }

    [Theory]
    [InlineData("deleted-report", null)]
    [InlineData(null, "deleted-view")]
    public async Task AReportThatIsGoneOrNotPublished_SaysSo(string reportId, string viewId)
    {
        // Arrange
        var fixture = new Fixture();
        var part = Fixture.Part();
        part.ReportId = reportId;
        part.ViewId = viewId;

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.NotConfigured, result.Outcome);
        Assert.True(result.IsMissing);
        Assert.Null(result.Document);
        Assert.Empty(fixture.Data.Queries);
    }

    [Fact]
    public async Task AReportWhoseOwnerIsGone_FailsWithTheReason_AndIsNotKept()
    {
        // Arrange
        var design = Sales();
        design.OwnerId = "former-employee";

        var fixture = new Fixture(design);
        var part = Fixture.Part();

        // Act
        var result = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);
        design.OwnerId = "owner";
        var retry = await fixture.Service.GetAsync(part, fixture.Manager, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportWidgetOutcome.Failed, result.Outcome);
        Assert.Contains("owner no longer has an active account", Assert.Single(result.Errors), StringComparison.Ordinal);
        Assert.Equal(ReportWidgetOutcome.Shown, retry.Outcome);
    }

    [Fact]
    public void APeriodWithoutBounds_TurnsTheFilterOff_AndAnOpenBoundIsLeftEmpty()
    {
        // Act
        var none = ReportRunPeriod.FromRange(null, null).ToFilterValues();
        var since = ReportRunPeriod.FromRange(new DateTime(2026, 2, 1, 0, 0, 0), null).ToFilterValues();
        var until = ReportRunPeriod.FromRange(null, new DateTime(2026, 2, 28).AddDays(1).AddTicks(-1)).ToFilterValues();

        // Assert
        Assert.Empty(none);
        Assert.Equal(["2026-02-01", string.Empty], since);
        Assert.Equal([string.Empty, "2026-02-28"], until);
    }

    // The order identifiers of the detail rows of the report's table.
    private static List<string> OrderIds(ReportDocument document)
    {
        return document.Sections
            .Single(section => section.Kind == ReportSectionKind.Table)
            .Rows
            .Where(row => row.Kind == ReportRowKind.Detail)
            .Select(row => row.Cells[0])
            .ToList();
    }

    // Sales per order, shared with the Managers role, with a date range filter its viewers can change.
    private static ReportDesign Sales()
    {
        return new ReportDesign
        {
            ItemId = "sales",
            DisplayText = "Sales",
            OwnerId = "owner",
            SharedRoles = ["Managers"],
            ModifiedUtc = _now.AddDays(-1),
            Query = OrdersQuery(),
            Visuals =
            [
                new ReportVisualDefinition { Id = "kpi", Type = ReportVisualType.Metrics, ValueColumnIds = ["total"] },
                new ReportVisualDefinition { Id = "table", Type = ReportVisualType.Table, ShowTotals = true },
                new ReportVisualDefinition { Id = "chart", Type = ReportVisualType.Chart, ChartType = ReportChartType.Bar, CategoryColumnId = "order", ValueColumnIds = ["total"] },
            ],
        };
    }

    // The filter a new report starts with in the builder: placed in the last 30 days, which its viewers can change.
    private static ReportFilterDefinition LastThirtyDays()
    {
        return new ReportFilterDefinition
        {
            Id = "placed",
            Field = "o.PlacedUtc",
            Operator = DataFilterOperator.InLastDays,
            Values = ["30"],
            Exposed = true,
            Label = "Placed",
            Control = ReportFilterControl.RelativeDate,
        };
    }

    private static ReportQueryDefinition OrdersQuery()
    {
        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" }],
            Columns = [Column("order", "o.Id"), Column("total", "o.Total", DataAggregate.Sum)],
            Sorts = [new ReportSortDefinition { ColumnId = "order" }],
            Filters =
            [
                new ReportFilterDefinition
                {
                    Id = "placed",
                    Field = "o.PlacedUtc",
                    Operator = DataFilterOperator.Between,
                    Values = ["2026-01-01", "2026-01-31"],
                    Exposed = true,
                    Label = "Placed",
                },
            ],
        };
    }

    private sealed class Fixture
    {
        public Fixture(ReportDesign design = null)
        {
            Data = SalesData();

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            var timeZone = new Mock<ITimeZone>();
            timeZone.SetupGet(value => value.TimeZoneId).Returns("UTC");

            var localClock = new Mock<ILocalClock>();
            localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);
            localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
                .ReturnsAsync((DateTimeOffset value) => value);

            var owner = new User { UserId = "owner", UserName = "Owner", IsEnabled = true };
            var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            userManager.Setup(manager => manager.FindByIdAsync("owner")).ReturnsAsync(owner);

            var principalFactory = new Mock<IUserClaimsPrincipalFactory<IUser>>();
            principalFactory.Setup(factory => factory.CreateAsync(owner)).ReturnsAsync(User("owner", "Owner"));

            Runner = new ReportDesignRunner(
                Planner(Data),
                Engine(Data),
                DocumentBuilder(),
                new ReportExecutionContextFactory(clock.Object, localClock.Object, Options.Create(new ReportQueryLimits())),
                new ReportOwnerPrincipalResolver(userManager.Object, principalFactory.Object),
                NullLogger<ReportDesignRunner>.Instance,
                new PassThroughStringLocalizer<ReportDesignRunner>());

            var view = new ReportView
            {
                ItemId = "orders-view",
                DisplayText = "Orders",
                OwnerId = "designer-1",
                ModifiedUtc = _now.AddDays(-2),
                Query = OrdersQuery(),
            };

            Service = new ReportDesignWidgetService(
                Catalog(design ?? Sales()),
                Catalog(view),
                Runner,
                new DesignerAuthorization("designer-1", "designer-2"),
                new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()),
                new MemoryCache(new MemoryCacheOptions()),
                localClock.Object,
                clock.Object,
                NullLogger<ReportDesignWidgetService>.Instance);
        }

        public InMemoryReportDataSource Data { get; }

        public ReportDesignRunner Runner { get; }

        public ReportDesignWidgetService Service { get; }

        // In the role the report is shared with, but doesn't design reports.
        public ClaimsPrincipal Manager { get; } = User("manager-1", "manager1", "Managers");

        public ClaimsPrincipal Stranger { get; } = User("stranger", "stranger", "Clerks");

        public ClaimsPrincipal Designer { get; } = User("designer-1", "designer1");

        public ClaimsPrincipal OtherDesigner { get; } = User("designer-2", "designer2");

        public static ReportDesignWidgetPart Part()
        {
            var part = new ReportDesignWidgetPart
            {
                ReportId = "sales",
                Period = ReportWidgetPeriod.Last30Days,
                Show = ReportWidgetContent.Everything,
                MaxRows = 10,
            };

            part.ContentItem = new ContentItem { ContentItemId = "widget-1", ModifiedUtc = _now.AddHours(-1) };

            return part;
        }
    }

    /// <summary>
    /// Grants the report builder's permission to design reports to the users a test lists, and runs the report
    /// builder's authorization handler, which grants a report to its owner and the people it is shared with.
    /// </summary>
    private sealed class DesignerAuthorization : IAuthorizationService
    {
        private readonly HashSet<string> _designers;
        private readonly ReportDesignAuthorizationHandler _handler;

        public DesignerAuthorization(params string[] designers)
        {
            _designers = new HashSet<string>(designers, StringComparer.Ordinal);
            _handler = new ReportDesignAuthorizationHandler(new Lazy<IAuthorizationService>(() => this));
        }

        public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var list = requirements.ToList();
            var context = new AuthorizationHandlerContext(list, user, resource);
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var requirement in list.OfType<PermissionRequirement>())
            {
                if (userId is not null &&
                    _designers.Contains(userId) &&
                    requirement.Permission.Name == ReportDesignerPermissions.ManageOwnReportDesigns.Name)
                {
                    context.Succeed(requirement);
                }
            }

            await _handler.HandleAsync(context);

            return context.HasSucceeded ? AuthorizationResult.Success() : AuthorizationResult.Failed();
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }
    }
}
