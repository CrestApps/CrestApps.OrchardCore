using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.BackgroundTasks;
using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Migrations;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.ReportDesignerPrincipals;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;

/// <summary>
/// Scheduled views: their stored result is refreshed in the background and read by reports instead of running the
/// view, over a real SQLite store whose tables the module's migration creates.
/// </summary>
public sealed class ReportViewSnapshotTests : IAsyncLifetime
{
    private static readonly DateTime _start = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ClaimsPrincipal _designer = User("designer", "Designer");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"report-view-snapshots-{Guid.NewGuid():N}.db");
    private readonly InMemoryReportDataSource _sales = SalesData();
    private readonly FlakyReportDataSource _flaky = new();
    private DateTime _now = _start;
    private bool _locked = true;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ReportViewSnapshotIndexProvider()]);

        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migrations = new ReportViewSnapshotMigrations
        {
            SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction),
        };

        Assert.Equal(1, await migrations.CreateAsync());

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        // Deleting retries while another test's connection still holds the file under a parallel run.
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task QueryAsync_ScheduledViewWithStoredResult_ReadsTheStoredRowsInsteadOfRunningTheView()
    {
        // Arrange
        await using var services = Services(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));
        await RefreshAsync(services, "v1");
        _sales.Queries.Clear();

        // Act
        var table = await QueryAsync(services, "v1", fields: ["revenue"], maxRows: 1);

        // Assert
        Assert.Empty(_sales.Queries);
        Assert.Equal(["revenue"], table.Fields.Select(field => field.Name));
        Assert.Equal(ReportDataType.Decimal, table.Fields[0].DataType);
        var row = Assert.Single(table.Rows);
        Assert.Equal(300m, Assert.IsType<decimal>(Assert.Single(row)));
        Assert.True(table.Truncated);
    }

    [Fact]
    public async Task QueryAsync_ScheduledViewWithStoredResult_ServesReportsBuiltOnTheView()
    {
        // Arrange
        await using var services = Services(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));
        await RefreshAsync(services, "v1");
        _sales.Queries.Clear();

        await using var scope = services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ReportQueryEngine>();
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "v", Source = ReportsConstants.ViewsDataSource, DataSet = "v1" }],
            Columns = [Column("region", "v.region"), Column("revenue", "v.revenue", ReportAggregate.Sum)],
            Sorts = [new ReportSortDefinition { ColumnId = "revenue", Descending = true }],
        };
        var context = Context(_now);
        context.DataSourceContext.User = _designer;

        // Act
        var result = await engine.ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_sales.Queries);
        Assert.Equal(["East", 300m], result.Rows[0]);
        Assert.Equal(["West", 170m], result.Rows[1]);
    }

    [Fact]
    public async Task QueryAsync_ScheduledViewWithoutStoredResult_RunsTheView()
    {
        // Arrange
        await using var services = Services(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));

        // Act
        var table = await QueryAsync(services, "v1");

        // Assert
        Assert.NotEmpty(_sales.Queries);
        Assert.Equal(["region", "revenue"], table.Fields.Select(field => field.Name));
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public async Task QueryAsync_StoredResultOfAnEarlierVersionOfTheView_IsIgnored()
    {
        // Arrange
        var view = RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly);
        var views = Catalog(view);
        await using var services = Services(views);
        await RefreshAsync(services, "v1");

        // The view changes outside the builder, which would have dropped the stored result.
        var changed = view.Clone();
        changed.Query.Columns.Add(Column("orders", "o.Id", ReportAggregate.Count));
        await views.UpdateAsync(changed, TestContext.Current.CancellationToken);
        _sales.Queries.Clear();

        // Act
        var table = await QueryAsync(services, "v1");

        // Assert
        Assert.NotEmpty(_sales.Queries);
        Assert.Equal(["region", "revenue", "orders"], table.Fields.Select(field => field.Name));
    }

    [Fact]
    public async Task BackgroundRefresh_RefreshesOnlyTheScheduledViewsThatAreDue()
    {
        // Arrange
        await using var services = Services(
            RevenueByRegion("never-refreshed", ReportViewRefreshIntervals.Hourly),
            RevenueByRegion("fresh", ReportViewRefreshIntervals.Hourly),
            RevenueByRegion("live", ReportViewRefreshIntervals.Live));
        await RefreshAsync(services, "fresh");
        _now = _start.AddMinutes(10);

        // Act
        await RunBackgroundAsync(services);

        // Assert
        var statuses = await StatusesAsync(services);
        Assert.Equal(_now, statuses["never-refreshed"].RefreshedUtc);
        Assert.Equal(_start, statuses["fresh"].RefreshedUtc);
        Assert.False(statuses.ContainsKey("live"));

        // Act: an hour after its refresh the fresh view is due too.
        _now = _start.AddMinutes(60);
        await RunBackgroundAsync(services);

        // Assert
        statuses = await StatusesAsync(services);
        Assert.Equal(_now, statuses["fresh"].RefreshedUtc);
        Assert.Equal(_start.AddMinutes(10), statuses["never-refreshed"].RefreshedUtc);
    }

    [Fact]
    public async Task BackgroundRefresh_FailingView_KeepsItsRowsAndRecordsTheError_AndTheOtherViewsStillRefresh()
    {
        // Arrange
        await using var services = Services(
            FlakyView("broken", ReportViewRefreshIntervals.Minimum),
            RevenueByRegion("healthy", ReportViewRefreshIntervals.Minimum));
        await RunBackgroundAsync(services);
        _flaky.Fail = true;
        _now = _start.AddMinutes(15);

        // Act
        await RunBackgroundAsync(services);

        // Assert
        var statuses = await StatusesAsync(services);
        Assert.Equal(_start, statuses["broken"].RefreshedUtc);
        Assert.Equal(_now, statuses["broken"].AttemptedUtc);
        Assert.Equal(_now, statuses["broken"].LastErrorUtc);
        Assert.Equal("The view could not be refreshed. The error was logged.", statuses["broken"].LastError);
        Assert.Equal(_now, statuses["healthy"].RefreshedUtc);
        Assert.Null(statuses["healthy"].LastError);

        // The reports that read the failing view keep reading its earlier rows.
        var table = await QueryAsync(services, "broken");
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(1, _flaky.FailedQueries);
    }

    [Fact]
    public async Task Refresh_ViewThatCannotRun_RecordsWhy()
    {
        // Arrange
        await using var services = Services(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));
        _sales.DeniedDataSets.Add("Order");

        // Act
        var result = await RefreshAsync(services, "v1");

        // Assert
        Assert.Equal(ReportViewRefreshStatus.Failed, result.Status);
        Assert.Null(result.Snapshot.RefreshedUtc);
        Assert.StartsWith("The view 'Revenue by region' cannot run:", result.Snapshot.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_WhenAnotherNodeIsRefreshingTheView_SkipsIt()
    {
        // Arrange
        await using var services = Services(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));
        _locked = false;

        // Act
        var result = await RefreshAsync(services, "v1");

        // Assert
        Assert.Equal(ReportViewRefreshStatus.Busy, result.Status);
        Assert.Empty(await StatusesAsync(services));
    }

    [Fact]
    public async Task Refresh_ViewWhoseOwnerIsGone_RecordsTheError()
    {
        // Arrange
        var view = RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly);
        view.OwnerId = "deleted-user";
        await using var services = Services(view);

        // Act
        var result = await RefreshAsync(services, "v1");

        // Assert
        Assert.Equal(ReportViewRefreshStatus.Failed, result.Status);
        Assert.Equal("The owner of the view 'Revenue by region' no longer exists or is disabled.", result.Snapshot.LastError);
    }

    [Fact]
    public async Task SaveViewAsync_ChangedQuery_DeletesTheStoredResult_ButARenameKeepsIt()
    {
        // Arrange
        var view = RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly);
        var views = Catalog(view);
        await using var services = Services(views);
        await RefreshAsync(services, "v1");

        // Act
        var renamed = await SaveViewAsync(services, views, existing =>
        {
            existing.DisplayText = "Revenue per region";
        });
        var afterRename = await StatusesAsync(services);
        var changed = await SaveViewAsync(services, views, existing =>
        {
            existing.Query.Columns.Add(Column("orders", "o.Id", ReportAggregate.Count));
        });

        // Assert
        Assert.True(renamed.Saved);
        Assert.True(afterRename.ContainsKey("v1"));
        Assert.True(changed.Saved);
        Assert.Empty(await StatusesAsync(services));
    }

    [Fact]
    public async Task SaveViewAsync_NormalizesTheSchedule_AndALiveViewLosesItsStoredResult()
    {
        // Arrange
        var view = RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly);
        var views = Catalog(view);
        await using var services = Services(views);
        await RefreshAsync(services, "v1");

        // Act
        await SaveViewAsync(services, views, existing => existing.RefreshIntervalMinutes = 5);
        var shortened = (await views.FindByIdAsync("v1", TestContext.Current.CancellationToken)).RefreshIntervalMinutes;
        var keptWhileScheduled = (await StatusesAsync(services)).ContainsKey("v1");
        await SaveViewAsync(services, views, existing => existing.RefreshIntervalMinutes = -1);

        // Assert
        Assert.Equal(ReportViewRefreshIntervals.Minimum, shortened);
        Assert.True(keptWhileScheduled);
        Assert.Equal(ReportViewRefreshIntervals.Live, (await views.FindByIdAsync("v1", TestContext.Current.CancellationToken)).RefreshIntervalMinutes);
        Assert.Empty(await StatusesAsync(services));
    }

    [Fact]
    public async Task DeleteViewAsync_DeletesTheStoredResult()
    {
        // Arrange
        var views = Catalog(RevenueByRegion("v1", ReportViewRefreshIntervals.Hourly));
        await using var services = Services(views);
        await RefreshAsync(services, "v1");

        // Act
        await using (var scope = services.CreateAsyncScope())
        {
            var designService = DesignService(scope.ServiceProvider, views);
            await designService.DeleteViewAsync(await views.FindByIdAsync("v1", TestContext.Current.CancellationToken));
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(await StatusesAsync(services));
    }

    [Fact]
    public async Task StoredValues_ReadBackWithTheClrTypesOfTheirFields()
    {
        // Arrange
        var placed = new DateTime(2026, 2, 5, 9, 30, 15, DateTimeKind.Utc);
        var table = new ReportDataTable
        {
            Fields =
            [
                new ReportFieldDescriptor("count", "Count", ReportDataType.Integer),
                new ReportFieldDescriptor("total", "Total", ReportDataType.Decimal),
                new ReportFieldDescriptor("active", "Active", ReportDataType.Boolean),
                new ReportFieldDescriptor("placed", "Placed", ReportDataType.DateTime),
                new ReportFieldDescriptor("name", "Name", ReportDataType.Text),
                new ReportFieldDescriptor("day", "Day", ReportDataType.Date),
            ],
            Rows =
            [
                [9_007_199_254_740_993L, 12.50m, true, placed, "Acme \"Corp\"", new DateTime(2026, 2, 5)],
                [0L, -0.125m, false, null, null, null],
            ],
        };
        var snapshot = new ReportViewSnapshot { ViewId = "v1", AttemptedUtc = _now, RefreshedUtc = _now };
        ReportViewSnapshotData.Fill(snapshot, table, maxRows: 100);

        await using (var session = _store.CreateSession())
        {
            await new ReportViewSnapshotStore(session).SaveAsync(snapshot, TestContext.Current.CancellationToken);
        }

        // Act
        ReportDataTable read;

        await using (var session = _store.CreateSession())
        {
            read = ReportViewSnapshotData.ToTable(await new ReportViewSnapshotStore(session).FindAsync("v1"), null, maxRows: 100);
        }

        // Assert
        var first = read.Rows[0];
        Assert.Equal(9_007_199_254_740_993L, Assert.IsType<long>(first[0]));
        Assert.Equal(12.50m, Assert.IsType<decimal>(first[1]));
        Assert.True(Assert.IsType<bool>(first[2]));
        var date = Assert.IsType<DateTime>(first[3]);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(placed, date);
        Assert.Equal("Acme \"Corp\"", Assert.IsType<string>(first[4]));
        Assert.Equal(new DateTime(2026, 2, 5), Assert.IsType<DateTime>(first[5]));

        var second = read.Rows[1];
        Assert.Equal(0L, Assert.IsType<long>(second[0]));
        Assert.Equal(-0.125m, Assert.IsType<decimal>(second[1]));
        Assert.False(Assert.IsType<bool>(second[2]));
        Assert.Null(second[3]);
        Assert.Null(second[4]);
        Assert.False(read.Truncated);
    }

    [Fact]
    public void DesignerPayload_CarriesTheRefreshSchedule()
    {
        // Arrange
        var view = RevenueByRegion("v1", ReportViewRefreshIntervals.EverySixHours);

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(ReportDesignerPayload.From(view), ReportDesignerJson.Options);
        var payload = System.Text.Json.JsonSerializer.Deserialize<ReportDesignerPayload>(json, ReportDesignerJson.Options);

        // Assert
        Assert.Contains("\"refreshIntervalMinutes\":360", json, StringComparison.Ordinal);
        Assert.Equal(ReportViewRefreshIntervals.EverySixHours, payload.ToView().RefreshIntervalMinutes);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-30, 0)]
    [InlineData(1, 15)]
    [InlineData(15, 15)]
    [InlineData(60, 60)]
    [InlineData(1440, 1440)]
    public void Normalize_ClampsSchedulesToTheShortestOne(int requested, int expected)
    {
        // Act & Assert
        Assert.Equal(expected, ReportViewRefreshIntervals.Normalize(requested));
    }

    // Orchard runs a background task with a scoped service provider of the tenant.
    private static async Task RunBackgroundAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        await new ReportViewSnapshotBackgroundTask().DoWorkAsync(scope.ServiceProvider, TestContext.Current.CancellationToken);
    }

    private static async Task<ReportViewRefreshResult> RefreshAsync(ServiceProvider services, string viewId)
    {
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ReportViewSnapshotRefresher>()
            .RefreshAsync(viewId, onlyWhenDue: false, TestContext.Current.CancellationToken);
    }

    private static async Task<ReportDataTable> QueryAsync(ServiceProvider services, string viewId, string[] fields = null, int maxRows = 1000)
    {
        await using var scope = services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetServices<IReportDataSource>().OfType<ReportViewsDataSource>().Single();

        return await source.QueryAsync(new ReportDataSourceQuery
        {
            DataSet = viewId,
            Fields = new HashSet<string>(fields ?? [], StringComparer.Ordinal),
            MaxRows = maxRows,
            Context = new ReportDataSourceContext { User = _designer },
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyDictionary<string, ReportViewSnapshotStatus>> StatusesAsync(ServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ReportViewSnapshotStore>().ListStatusesAsync();
    }

    private static async Task<ReportSaveResult> SaveViewAsync(ServiceProvider services, Catalog<ReportView> views, Action<ReportView> change)
    {
        await using var scope = services.CreateAsyncScope();
        var existing = await views.FindByIdAsync("v1", TestContext.Current.CancellationToken);
        var incoming = existing.Clone();

        change(incoming);

        var result = await DesignService(scope.ServiceProvider, views).SaveViewAsync(incoming, existing.Clone(), _designer);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private static ReportDesignService DesignService(IServiceProvider services, Catalog<ReportView> views)
    {
        var clock = services.GetRequiredService<IClock>();

        return new ReportDesignService(
            Catalog<ReportDesign>(),
            views,
            new ReportShareLinkService(Catalog<ReportShareLink>(), clock),
            services.GetRequiredService<ReportViewSnapshotStore>(),
            services.GetRequiredService<ReportQueryPlanner>(),
            DocumentBuilder(),
            clock,
            new PassThroughStringLocalizer<ReportDesignService>());
    }

    private ServiceProvider Services(params ReportView[] views)
    {
        return Services(Catalog(views));
    }

    private ServiceProvider Services(Catalog<ReportView> views)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => _now);

        var timeZone = new Mock<ITimeZone>();
        timeZone.SetupGet(value => value.TimeZoneId).Returns("UTC");

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => (_locked ? Mock.Of<ILocker>() : null, _locked));

        var owner = new User { UserId = "owner", UserName = "Owner", IsEnabled = true };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByIdAsync("owner")).ReturnsAsync(owner);

        var principalFactory = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principalFactory.Setup(factory => factory.CreateAsync(owner)).ReturnsAsync(User("owner", "Owner"));

        services.AddSingleton(clock.Object);
        services.AddSingleton(localClock.Object);
        services.AddSingleton(distributedLock.Object);
        services.AddSingleton(userManager.Object);
        services.AddSingleton(principalFactory.Object);
        services.AddSingleton<IAuthorizationService>(new DesignersOnly("designer", "owner"));
        services.AddSingleton(_store);
        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton<ICatalog<ReportView>>(views);
        services.AddSingleton(Formatter);
        services.AddSingleton<IReportDataSource>(_sales);
        services.AddSingleton<IReportDataSource>(_flaky);

        services
            .AddScoped<IReportDataSource, ReportViewsDataSource>()
            .AddScoped<IReportDataSourceManager, ReportDataSourceManager>()
            .AddScoped<ReportQueryPlanner>()
            .AddScoped<ReportQueryEngine>()
            .AddScoped(sp => new Lazy<ReportQueryPlanner>(sp.GetRequiredService<ReportQueryPlanner>))
            .AddScoped(sp => new Lazy<ReportQueryEngine>(sp.GetRequiredService<ReportQueryEngine>))
            .AddScoped<ReportExecutionContextFactory>()
            .AddScoped<ReportOwnerPrincipalResolver>()
            .AddScoped<ReportViewRunner>()
            .AddScoped<ReportViewSnapshotStore>()
            .AddScoped<ReportViewSnapshotRefresher>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ReportView RevenueByRegion(string id, int refreshIntervalMinutes)
    {
        var query = CustomersWithOrders();
        query.Columns = [Column("region", "c.Region"), Column("revenue", "o.Total", ReportAggregate.Sum)];
        query.Sorts = [new ReportSortDefinition { ColumnId = "revenue", Descending = true }];

        return new ReportView
        {
            ItemId = id,
            DisplayText = "Revenue by region",
            OwnerId = "owner",
            RefreshIntervalMinutes = refreshIntervalMinutes,
            Query = query,
        };
    }

    private static ReportView FlakyView(string id, int refreshIntervalMinutes)
    {
        return new ReportView
        {
            ItemId = id,
            DisplayText = "Flaky",
            OwnerId = "owner",
            RefreshIntervalMinutes = refreshIntervalMinutes,
            Query = new ReportQueryDefinition
            {
                DataSets = [new ReportDataSetReference { Alias = "t", Source = FlakyReportDataSource.SourceName, DataSet = "Things" }],
                Columns = [Column("name", "t.Name")],
            },
        };
    }

    /// <summary>
    /// A data source whose rows can start failing, as a database that went away would.
    /// </summary>
    private sealed class FlakyReportDataSource : IReportDataSource
    {
        public const string SourceName = "Flaky";

        private readonly InMemoryReportDataSource _inner = new InMemoryReportDataSource(SourceName)
            .Add("Things", [new ReportFieldDescriptor("Name", "Name", ReportDataType.Text)], ["first"], ["second"]);

        public bool Fail { get; set; }

        public int FailedQueries { get; private set; }

        public string Name => SourceName;

        public LocalizedString DisplayName => _inner.DisplayName;

        public LocalizedString Description => _inner.Description;

        public Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
        {
            return _inner.GetDataSetsAsync(context, cancellationToken);
        }

        public Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
        {
            return _inner.GetSchemaAsync(dataSet, context, cancellationToken);
        }

        public Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                FailedQueries++;

                throw new InvalidOperationException("The database is not reachable.");
            }

            return _inner.QueryAsync(query, cancellationToken);
        }
    }

    /// <summary>
    /// Grants every permission to the listed users only.
    /// </summary>
    private sealed class DesignersOnly : IAuthorizationService
    {
        private readonly HashSet<string> _userIds;

        public DesignersOnly(params string[] userIds)
        {
            _userIds = new HashSet<string>(userIds, StringComparer.Ordinal);
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Task.FromResult(userId is not null && _userIds.Contains(userId) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }
    }
}
