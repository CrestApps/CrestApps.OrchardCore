using System.Security.Claims;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.AI;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Handlers;
using CrestApps.OrchardCore.Reports.Designer.Indexes;
using CrestApps.OrchardCore.Reports.Designer.Migrations;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.DisplayManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ReportsAuthorizationService = CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.FakeAuthorizationService;
using YesSqlSession = YesSql.ISession;
using YesSqlStore = YesSql.IStore;
using YesSqlStoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.AI;

/// <summary>
/// Builds what a Reports tool needs on the real report services: a built-in sales report, the designed reports, views
/// and groups in in-memory catalogs, drafts and versions in a SQLite database the module's migrations create, the sales
/// data set, share links in memory, and the report authorization handler, which grants a report to its owner and the
/// people and roles it is shared with.
/// </summary>
internal sealed class ReportToolHarness : IAsyncDisposable
{
    public const string CallerId = "caller";
    public const string CallerName = "Ada";
    public const string OwnerId = "owner";

    public static readonly DateTime Now = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"report-tools-{Guid.NewGuid():N}.db");
    private YesSqlStore _store;
    private YesSqlSession _session;

    public Catalog<ReportDesign> Designs { get; } = ReportDesignerPrincipals.Catalog<ReportDesign>();

    public Catalog<ReportView> Views { get; } = ReportDesignerPrincipals.Catalog<ReportView>();

    public Catalog<ReportGroup> Groups { get; } = ReportDesignerPrincipals.Catalog<ReportGroup>();

    public InMemorySharedLinkManager SharedLinks { get; } = new();

    public List<string> Paths { get; } = [];

    public static async Task<ReportToolHarness> CreateAsync()
    {
        var harness = new ReportToolHarness();
        var cancellationToken = TestContext.Current.CancellationToken;

        harness._store = YesSqlStoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={harness._databasePath};Pooling=False"));
        harness._store.RegisterIndexes([new ReportDesignDraftIndexProvider(), new ReportDesignVersionIndexProvider(), new ReportViewSnapshotIndexProvider()]);
        await harness._store.InitializeAsync(cancellationToken);

        await using (var session = harness._store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var history = new ReportDesignHistoryMigrations
            {
                SchemaBuilder = new SchemaBuilder(harness._store.Configuration, transaction),
            };
            var snapshots = new ReportViewSnapshotMigrations
            {
                SchemaBuilder = new SchemaBuilder(harness._store.Configuration, transaction),
            };

            await history.CreateAsync();
            await history.UpdateFrom1Async();
            await snapshots.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        harness._session = harness._store.CreateSession();

        return harness;
    }

    public async ValueTask DisposeAsync()
    {
        await _session.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);
    }

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <param name="signedIn">Whether the caller is signed in.</param>
    /// <param name="granted">The permissions the caller holds, by name.</param>
    /// <returns>The services.</returns>
    public ServiceProvider Build(bool signedIn = true, params string[] granted)
    {
        var httpContext = new DefaultHttpContext();

        if (signedIn)
        {
            httpContext.User = ReportDesignerPrincipals.User(CallerId, CallerName);
        }

        var services = new ServiceCollection();
        var authorization = new ReportsAuthorizationService(granted);

        authorization.With(new ReportDesignAuthorizationHandler(new Lazy<IAuthorizationService>(() => authorization)));

        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IAuthorizationService>(authorization);
        services.AddSingleton(CreateLocalClock());
        services.AddSingleton(CreateClock());
        services.AddSingleton<IStringLocalizerFactory>(new PassThroughStringLocalizerFactory());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(CreateLinkGenerator());
        services.AddScoped<ReportToolScopeChanges>();

        // Built-in reports.
        services.AddSingleton<SalesTestReport>();
        services.AddSingleton(Options.Create(new ReportOptions().Add(ReportDescriptor.Create<SalesTestReport>())));
        services.AddSingleton(Options.Create(new ReportExportFormatOptions().Add(ReportExportFormatDescriptor.Create<CsvReportExportFormat>())));
        services.AddSingleton<IReportManager, ReportManager>();
        services.AddSingleton<IReportExportManager, ReportExportManager>();
        services.AddSingleton(new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()));
        services.AddSingleton<ReportDateRangeResolver>();

        // The Report Builder.
        var data = ReportDesignerTestServices.SalesData();
        var dataSources = new TestDataSourceManager([new TestDataSourceEntry(data.SourceName, data)]);

        services.AddSingleton<IDataSourceManager>(dataSources);
        services.AddSingleton<IExpressionFunctionCatalog>(ExpressionFunctionCatalog.BuiltIn);
        services.AddSingleton<global::CrestApps.Core.Services.ICatalog<ReportDesign>>(Designs);
        services.AddSingleton<global::CrestApps.Core.Services.ICatalog<ReportView>>(Views);
        services.AddSingleton<global::CrestApps.Core.Services.ICatalog<ReportGroup>>(Groups);
        services.AddSingleton<global::OrchardCore.DataPipelines.SharedLinks.ISharedLinkManager>(SharedLinks);
        services.AddSingleton(_session);
        services.AddSingleton(new ReportViewSnapshotStore(_session));
        services.AddSingleton(ReportDesignerTestServices.Planner(data));
        services.AddSingleton(ReportDesignerTestServices.DocumentBuilder());
        services.AddSingleton<ReportDesignService>();
        services.AddSingleton<ReportGroupService>();
        services.AddSingleton(new ReportDesignHistoryStore(_session));
        services.AddSingleton(CreateLock());
        services.AddSingleton(Mock.Of<IReportDesignNotifier>());
        services.AddSingleton(Options.Create(new ReportDesignVersionOptions()));
        services.AddSingleton<ReportDesignHistoryService>();
        services.AddSingleton(ReportDesignRunnerTests.Runner(data));
        services.AddSingleton<ReportShareLinkService>();
        services.AddSingleton(new ReportViewSnapshotRefresher(
            Views,
            new ReportViewSnapshotStore(_session),
            null,
            null,
            authorization,
            CreateLock(),
            Options.Create(new ReportQueryLimits()),
            CreateClock(),
            NullLogger<ReportViewSnapshotRefresher>.Instance,
            new PassThroughStringLocalizer<ReportViewSnapshotRefresher>()));

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => string.Equals(name, "jane", StringComparison.OrdinalIgnoreCase)
                ? new User { UserId = "jane-id", UserName = "Jane", IsEnabled = true }
                : null);
        services.AddSingleton(userManager.Object);

        var roleService = new Mock<IRoleService>();
        roleService.Setup(service => service.GetRolesAsync()).ReturnsAsync(() => new[] { "Administrator", "Sales Manager" }.Select(name => Mock.Of<IRole>(role => role.RoleName == name)).ToList());
        services.AddSingleton(roleService.Object);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A published report owned by someone else, reading the sales data.
    /// </summary>
    public static ReportDesign Report(string id = "r1", string title = "Sales by customer", string ownerId = OwnerId)
    {
        var query = ReportDesignerTestServices.CustomersWithOrders();

        query.Columns =
        [
            ReportDesignerTestServices.Column("name", "c.Name"),
            ReportDesignerTestServices.Column("total", "o.Total", global::OrchardCore.DataSources.DataAggregate.Sum),
        ];

        return new ReportDesign
        {
            ItemId = id,
            DisplayText = title,
            OwnerId = ownerId,
            Author = "Owner",
            CreatedUtc = Now.AddDays(-10),
            Query = query,
            AllowExport = true,
        };
    }

    private static ILocalClock CreateLocalClock()
    {
        var timeZone = new Mock<ITimeZone>();
        timeZone.Setup(zone => zone.TimeZoneId).Returns("UTC");

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
        localClock.Setup(clock => clock.ConvertToUtcAsync(It.IsAny<DateTime>())).ReturnsAsync((DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        return localClock.Object;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(Now);

        return clock.Object;
    }

    private static IDistributedLock CreateLock()
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(() => (Mock.Of<ILocker>(), true));

        return distributedLock.Object;
    }

    // Builds paths as the admin routes do, so a test can read the export address a tool returns.
    private static LinkGenerator CreateLinkGenerator()
    {
        var linkGenerator = new Mock<LinkGenerator>();
        linkGenerator
            .Setup(generator => generator.GetPathByAddress(
                It.IsAny<HttpContext>(),
                It.IsAny<RouteValuesAddress>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(),
                It.IsAny<LinkOptions>()))
            .Returns((HttpContext _, RouteValuesAddress address, RouteValueDictionary _, RouteValueDictionary _, PathString? _, FragmentString _, LinkOptions _) =>
            {
                var values = address.ExplicitValues;

                return address.RouteName switch
                {
                    "ReportsExport" => $"/Admin/Reports/view/{values["id"]}/export/{values["format"]}",
                    "ReportDesignsExport" => $"/Admin/reports/designs/{values["id"]}/export/{values["format"]}",
                    "ReportDesignsRun" => $"/Admin/reports/designs/{values["id"]}",
                    _ => null,
                };
            });

        return linkGenerator.Object;
    }
}

/// <summary>
/// A built-in report that lists one row per day of its period, so a test can see the period it ran on and the row cap.
/// </summary>
internal sealed class SalesTestReport : IReport
{
    public static string Name => "salesTest";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SalesTestReport>("Sales test");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SalesTestReport>("Sales by day.");

    public static string Category => ReportsConstants.Categories.Commerce;

    public static Permission Permission { get; } = new("ViewSalesTestReport", LocalizationSource.Create<SalesTestReport>("View sales test report"));

    public static Permission ExportPermission { get; } = new("ExportSalesTestReport", LocalizationSource.Create<SalesTestReport>("Export sales test report"));

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(7);

    public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var rows = new List<ReportRow>();

        for (var day = range.FromUtc ?? Now(); day <= (range.ToUtc ?? Now()); day = day.AddDays(1))
        {
            rows.Add(new ReportRow([day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), "10"]));
        }

        return Task.FromResult(new ReportDocument()
            .Add(ReportSection.ForMetrics("Totals", [new ReportMetric("Days", rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))]))
            .Add(ReportSection.ForTable("By day", [new ReportColumn("Day"), new ReportColumn("Sales")], rows)));
    }

    private static DateTime Now() => ReportToolHarness.Now;
}
