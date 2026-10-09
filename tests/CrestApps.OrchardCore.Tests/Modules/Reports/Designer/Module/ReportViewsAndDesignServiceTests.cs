using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Handlers;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

public sealed class ReportViewsAndDesignServiceTests
{
    private static readonly DateTime _now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ClaimsPrincipal _designer = User("designer", "Designer");

    [Fact]
    public async Task View_CanBeReadByAReportAsADataSet()
    {
        // Arrange
        var view = RevenueByRegionView("v1");
        var (engine, _) = Build(view);
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "v", Source = ReportsConstants.ViewsDataSource, DataSet = "v1" }],
            Columns =
            [
                Column("region", "v.region"),
                Column("revenue", "v.revenue", ReportAggregate.Sum),
            ],
            Sorts = [new ReportSortDefinition { ColumnId = "revenue", Descending = true }],
        };

        // Act
        var result = await engine.ExecuteAsync(query, ContextFor(_designer), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["East", 300m], result.Rows[0]);
        Assert.Equal(["West", 170m], result.Rows[1]);
    }

    // The engine shows date-times in the tenant time zone and expects sources to return UTC, so a view must hand its
    // date-times back in UTC or every report built on it would shift them twice.
    [Fact]
    public async Task View_ReturnsDateTimesInUtc()
    {
        // Arrange
        var view = new ReportView
        {
            ItemId = "v2",
            DisplayText = "Last order",
            OwnerId = "designer",
            Query = WithColumns(Column("name", "c.Name"), Column("last", "o.PlacedUtc", ReportAggregate.Max)),
        };
        var (_, source) = Build(view);

        // Act
        var table = await source.QueryAsync(new ReportDataSourceQuery
        {
            DataSet = "v2",
            MaxRows = 100,
            Context = new ReportDataSourceContext { User = _designer },
        }, TestContext.Current.CancellationToken);

        // Assert
        var acme = table.Rows.Single(row => (string)row[0] == "Acme");
        var last = Assert.IsType<DateTime>(acme[1]);
        Assert.Equal(DateTimeKind.Utc, last.Kind);
        Assert.Equal(new DateTime(2026, 2, 5, 9, 0, 0), last);
    }

    [Fact]
    public async Task Views_ThatReadEachOther_AreRefusedInsteadOfLoopingForever()
    {
        // Arrange
        var first = new ReportView
        {
            ItemId = "a",
            DisplayText = "A",
            Query = new ReportQueryDefinition
            {
                DataSets = [new ReportDataSetReference { Alias = "b", Source = ReportsConstants.ViewsDataSource, DataSet = "b" }],
                Columns = [Column("x", "b.x")],
            },
        };
        var second = new ReportView
        {
            ItemId = "b",
            DisplayText = "B",
            Query = new ReportQueryDefinition
            {
                DataSets = [new ReportDataSetReference { Alias = "a", Source = ReportsConstants.ViewsDataSource, DataSet = "a" }],
                Columns = [Column("x", "a.x")],
            },
        };
        var (engine, _) = Build(first, second);
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "a", Source = ReportsConstants.ViewsDataSource, DataSet = "a" }],
            Columns = [Column("x", "a.x")],
        };

        // Act
        var exception = await Assert.ThrowsAsync<ReportQueryException>(() => engine.ExecuteAsync(query, ContextFor(_designer), TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("reads itself", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Views_AreHiddenFromPeopleWhoCannotDesign()
    {
        // Arrange
        var (_, source) = Build(RevenueByRegionView("v1"));
        var context = new ReportDataSourceContext { User = User("viewer", "Viewer") };

        // Act & Assert
        Assert.Empty(await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken));
        Assert.Null(await source.GetSchemaAsync("v1", context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_NewReport_IsOwnedByTheDesignerAndKeptEvenWhenUnfinished()
    {
        // Arrange
        var (service, designs) = DesignService();

        // Act
        var result = await service.SaveAsync(new ReportDesign { DisplayText = " Revenue " }, null, _designer, canSharePublicly: false);

        // Assert
        Assert.True(result.Saved);
        Assert.Contains("Add at least one data set.", result.Warnings);
        var saved = Assert.Single(await designs.GetAllAsync(TestContext.Current.CancellationToken));
        Assert.Equal("designer", saved.OwnerId);
        Assert.Equal("Designer", saved.Author);
        Assert.Equal("Revenue", saved.DisplayText);
        Assert.Equal(_now, saved.CreatedUtc);
    }

    [Fact]
    public async Task SaveAsync_WithoutTitle_IsRefused()
    {
        // Arrange
        var (service, designs) = DesignService();

        // Act
        var result = await service.SaveAsync(new ReportDesign(), null, _designer, canSharePublicly: false);

        // Assert
        Assert.False(result.Saved);
        Assert.Empty(await designs.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_SharingWithAnonymousVisitors_NeedsThePublicSharingPermission()
    {
        // Arrange
        var (service, _) = DesignService();
        var report = new ReportDesign { DisplayText = "Public", SharedRoles = ["Anonymous"] };

        // Act
        var refused = await service.SaveAsync(report, null, _designer, canSharePublicly: false);
        var allowed = await service.SaveAsync(report, null, _designer, canSharePublicly: true);

        // Assert
        Assert.False(refused.Saved);
        Assert.Contains("You are not allowed to share reports with anonymous visitors.", refused.Errors);
        Assert.True(allowed.Saved);
    }

    [Fact]
    public async Task SaveAsync_ReportAlreadyPublic_CanStillBeEditedByItsOwnerWithoutThePublicSharingPermission()
    {
        // Arrange
        var existing = new ReportDesign { ItemId = "r1", DisplayText = "Public", OwnerId = "designer", SharedRoles = ["Anonymous"] };
        var (service, _) = DesignService(existing);

        // Act
        var result = await service.SaveAsync(new ReportDesign { DisplayText = "Public, renamed", SharedRoles = ["Anonymous"] }, existing.Clone(), _designer, canSharePublicly: false);

        // Assert
        Assert.True(result.Saved);
        Assert.Equal("r1", result.Id);
    }

    [Fact]
    public async Task SaveViewAsync_ViewThatReadsItself_IsRefused()
    {
        // Arrange
        var existing = new ReportView { ItemId = "v9", DisplayText = "Loop", OwnerId = "designer" };
        var (service, _) = DesignService(views: [existing]);
        var incoming = new ReportView
        {
            DisplayText = "Loop",
            Query = new ReportQueryDefinition
            {
                DataSets = [new ReportDataSetReference { Alias = "self", Source = ReportsConstants.ViewsDataSource, DataSet = "v9" }],
            },
        };

        // Act
        var result = await service.SaveViewAsync(incoming, existing.Clone(), _designer);

        // Assert
        Assert.False(result.Saved);
        Assert.Contains("A view cannot read itself.", result.Errors);
    }

    [Fact]
    public async Task FindViewUsagesAsync_ListsTheReportsAndViewsThatReadAView()
    {
        // Arrange
        var reader = new ReportDesign
        {
            ItemId = "r1",
            DisplayText = "Uses the view",
            Query = new ReportQueryDefinition { DataSets = [new ReportDataSetReference { Alias = "v", Source = ReportsConstants.ViewsDataSource, DataSet = "v1" }] },
        };
        var (service, _) = DesignService(reader);

        // Act
        var usages = await service.FindViewUsagesAsync("v1");

        // Assert
        Assert.Equal(["Uses the view"], usages);
    }

    private static ReportView RevenueByRegionView(string id)
    {
        return new ReportView
        {
            ItemId = id,
            DisplayText = "Revenue by region",
            OwnerId = "designer",
            Query = WithColumns(Column("region", "c.Region"), Column("revenue", "o.Total", ReportAggregate.Sum)),
        };
    }

    private static ReportQueryDefinition WithColumns(params ReportColumnDefinition[] columns)
    {
        var query = CustomersWithOrders();
        query.Columns = columns;

        return query;
    }

    private static ReportQueryExecutionContext ContextFor(ClaimsPrincipal user)
    {
        var context = Context(_now);
        context.DataSourceContext.User = user;

        return context;
    }

    private static (ReportQueryEngine Engine, ReportViewsDataSource Source) Build(params ReportView[] views)
    {
        ReportQueryPlanner planner = null;
        ReportQueryEngine engine = null;

        var authorization = DesignerAuthorization();
        var source = new ReportViewsDataSource(
            Catalog(views),
            authorization,
            new Lazy<ReportQueryPlanner>(() => planner),
            new Lazy<ReportQueryEngine>(() => engine),
            ContextFactory(),
            new PassThroughStringLocalizer<ReportViewsDataSource>());

        planner = new ReportQueryPlanner(new ReportDataSourceManager([SalesData(), source]), new PassThroughStringLocalizer<ReportQueryPlanner>());
        engine = new ReportQueryEngine(planner, Formatter, new PassThroughStringLocalizer<ReportQueryEngine>());

        return (engine, source);
    }

    private static FakeAuthorizationService DesignerAuthorization()
    {
        // Only the designer holds the designer permission; the handler turns it into access to views.
        FakeAuthorizationService service = null;
        var handler = new ReportDesignAuthorizationHandler(new Lazy<IAuthorizationService>(() => new DesignerOnly()));

        service = new FakeAuthorizationService().With(handler);

        return service;
    }

    private static ReportExecutionContextFactory ContextFactory()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var timeZone = new Mock<ITimeZone>();
        timeZone.SetupGet(value => value.TimeZoneId).Returns("UTC");

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);

        return new ReportExecutionContextFactory(clock.Object, localClock.Object, Options.Create(new ReportQueryLimits()));
    }

    private static (ReportDesignService Service, CrestApps.Core.Services.ICatalog<ReportDesign> Designs) DesignService(ReportDesign existing = null, ReportView[] views = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var designs = existing is null ? Catalog<ReportDesign>() : Catalog(existing);
        var service = new ReportDesignService(
            designs,
            Catalog(views ?? []),
            new ReportShareLinkService(Catalog<ReportShareLink>(), clock.Object),
            Planner(SalesData()),
            DocumentBuilder(),
            clock.Object,
            new PassThroughStringLocalizer<ReportDesignService>());

        return (service, designs);
    }

    private sealed class DesignerOnly : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var isDesigner = user?.FindFirstValue(ClaimTypes.NameIdentifier) == "designer";

            return Task.FromResult(isDesigner ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }
    }
}
