using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Migrations;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Reports.DataSources;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Downloads;

/// <summary>
/// The downloads data source reads real download entitlements from a SQLite store, so the index query, the fields it
/// exposes, the push-down of dates and join keys, the grouping in the database, and the permission it requires are all
/// exercised end to end.
/// </summary>
public sealed class DownloadReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"download-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new DownloadEntitlementIndexProvider()], DownloadsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(DownloadsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await Migrations(schemaBuilder).CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Four entitlements, one a day: one used up, one expired, one taken back, and one that still works.
            await session.SaveAsync(Entitlement(0, "order-1", "guide.pdf", count: 3, limit: 3, expires: null, revoked: null), false, DownloadsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Entitlement(1, "order-1", "notes.txt", count: 0, limit: 5, expires: _start.AddDays(5), revoked: null), false, DownloadsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Entitlement(2, "order-2", "guide.pdf", count: 1, limit: 3, expires: null, revoked: _start.AddDays(4)), false, DownloadsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Entitlement(3, "order-3", "guide.pdf", count: 1, limit: null, expires: null, revoked: null), false, DownloadsConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageDownloads_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.DownloadSource.Entitlements, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.DownloadSource.Entitlements), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.DownloadSource.Entitlements);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.DownloadSource.Entitlements, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesWhereTheFileIsKept()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.DownloadSource.Entitlements, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CommerceReportDataSets.DownloadSource.Entitlements], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(DownloadEntitlementsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField("OrderId").IsKeyFilterable);
        Assert.False(schema.FindField("ProductId").IsKeyFilterable);

        var order = Assert.Single(schema.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, order.Field);

        var product = Assert.Single(schema.FindField("ProductId").References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);

        var line = Assert.Single(schema.FindField("OrderLineId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.OrderLines, line.DataSet);
        Assert.Equal("LineId", line.Field);

        Assert.DoesNotContain(schema.Fields, field => field.Name is "FileId" or "RevokeReason" or "Url" or "Path");
    }

    [Fact]
    public async Task Query_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.DownloadSource.Entitlements), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["entitlement-3", "entitlement-2", "entitlement-1", "entitlement-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["Active", "Revoked", "Expired", "LimitReached"], rows.Select(row => row["Status"]));

        var usedUp = rows[3];
        Assert.Equal("order-1", usedUp["OrderId"]);
        Assert.Equal("N-1", usedUp["OrderNumber"]);
        Assert.Equal("line-0", usedUp["OrderLineId"]);
        Assert.Equal("product-ebook", usedUp["ProductId"]);
        Assert.Equal("Trail guide", usedUp["ProductTitle"]);
        Assert.Equal("guide.pdf", usedUp["FileName"]);
        Assert.Equal(3L, usedUp["DownloadCount"]);
        Assert.Equal(3L, usedUp["DownloadLimit"]);
        Assert.Equal(0L, usedUp["Remaining"]);
        Assert.Equal(false, usedUp["IsRevoked"]);
        Assert.Equal(_start, usedUp["CreatedUtc"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(usedUp["LastDownloadedUtc"]).Kind);

        var revoked = rows[1];
        Assert.Equal(true, revoked["IsRevoked"]);
        Assert.Equal(_start.AddDays(4), revoked["RevokedUtc"]);

        var unlimited = rows[0];
        Assert.Null(unlimited["DownloadLimit"]);
        Assert.Null(unlimited["Remaining"]);
        Assert.Null(unlimited["ExpiresUtc"]);
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.DownloadSource.Entitlements, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(DownloadEntitlementsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.DownloadSource.Entitlements, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = ["order-1"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.DownloadSource.Entitlements, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["entitlement-2", "entitlement-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["entitlement-1", "entitlement-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsByOrder_InTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.DownloadSource.Entitlements,
            Groups = [new DataAggregateGroup { Field = "OrderId" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(2L, counts["order-1"]);
        Assert.Equal(1L, counts["order-2"]);
        Assert.Equal(1L, counts["order-3"]);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var first = await Migrations(schemaBuilder).UpdateFrom1Async();
        var second = await Migrations(schemaBuilder).UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([DownloadsPermissions.ManageDownloads.Name]));
        services.AddSingleton(Clock());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<DownloadReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.DownloadSource.Name, DownloadReportDataSource.Name);
        Assert.Equal([CommerceReportDataSets.DownloadSource.Entitlements], dataSets.Select(dataSet => dataSet.Name));
    }

    private static DownloadEntitlement Entitlement(int day, string orderId, string fileName, int count, int? limit, DateTime? expires, DateTime? revoked)
    {
        var given = _start.AddDays(day);

        return new DownloadEntitlement
        {
            ItemId = $"entitlement-{day}",
            OrderId = orderId,
            OrderNumber = "N-" + orderId[^1..],
            OrderLineId = $"line-{day}",
            ProductId = "product-ebook",
            ProductTitle = "Trail guide",
            FileId = $"file{day}secret",
            FileName = fileName,
            DownloadCount = count,
            DownloadLimit = limit,
            ExpiresUtc = expires,
            RevokedUtc = revoked,
            RevokeReason = revoked.HasValue ? "Taken back by admin." : null,
            CreatedUtc = given,
            LastDownloadedUtc = count > 0 ? given.AddHours(1) : null,
        };
    }

    private static DownloadMigrations Migrations(ISchemaBuilder schemaBuilder)
    {
        return new DownloadMigrations(new Mock<IContentDefinitionManager>().Object)
        {
            SchemaBuilder = schemaBuilder,
        };
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();

        clock.SetupGet(value => value.UtcNow).Returns(_start.AddDays(10));

        return clock.Object;
    }

    private static DownloadReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [DownloadsPermissions.ManageDownloads.Name]);

        return new DownloadReportDataSource(
        [
            new DownloadEntitlementsReportDataSet(session, authorizationService, Clock(), new PassThroughStringLocalizer<DownloadEntitlementsReportDataSet>()),
        ]);
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
