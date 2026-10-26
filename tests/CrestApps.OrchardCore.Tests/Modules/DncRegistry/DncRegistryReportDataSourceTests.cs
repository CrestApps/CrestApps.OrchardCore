using System.Security.Claims;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Migrations;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// The DNC registry data source reads real local do-not-call lists and entries from a SQLite store built by the
/// module's own index providers and migration, so the index queries, the fields they expose, the push-down of dates,
/// join keys and statuses, the grouping in the database, and the permission they require are all exercised end to end.
/// </summary>
public sealed class DncRegistryReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Compliance");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"dnc-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new LocalDncListIndexProvider(), new LocalDncEntryIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(DncRegistryConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new LocalDncRegistryMigrations(NullLogger<LocalDncRegistryMigrations>.Instance) { SchemaBuilder = schemaBuilder }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // An imported US list with one rejected row, and a Canadian list still importing a day later.
            await session.SaveAsync(new LocalDncList
            {
                ListId = "list-1",
                CountryCode = "US",
                Name = "Internal opt-outs",
                UploadedFileName = "opt-outs.csv",
                StoredFileName = "stored-secret.csv",
                PhoneNumberCount = 3,
                TotalRecords = 4,
                TotalProcessed = 4,
                ImportedCount = 3,
                ErrorMessages = new Dictionary<int, string> { [5] = "Row ignored because it does not contain a valid phone number." },
                Status = LocalDncListStatus.Completed,
                CreatedUtc = _start,
                ProcessSaveUtc = _start.AddMinutes(5),
                CompletedUtc = _start.AddMinutes(5),
            }, false, DncRegistryConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new LocalDncList
            {
                ListId = "list-2",
                CountryCode = "CA",
                UploadedFileName = "canada.csv",
                StoredFileName = "stored-secret-2.csv",
                PhoneNumberCount = 1,
                TotalRecords = 10,
                TotalProcessed = 1,
                ImportedCount = 1,
                ErrorMessages = [],
                Status = LocalDncListStatus.Processing,
                FailedAttempts = 1,
                Error = "The upload was interrupted.",
                CreatedUtc = _start.AddDays(1),
            }, false, DncRegistryConstants.CollectionName, cancellationToken);

            string[] numbers = ["+14255551212", "+12065550100", "+12065550101"];

            for (var index = 0; index < numbers.Length; index++)
            {
                await session.SaveAsync(new LocalDncEntry
                {
                    EntryId = $"entry-{index + 1}",
                    ListId = "list-1",
                    CountryCode = "US",
                    PhoneNumber = numbers[index],
                }, false, DncRegistryConstants.CollectionName, cancellationToken);
            }

            await session.SaveAsync(new LocalDncEntry
            {
                EntryId = "entry-4",
                ListId = "list-2",
                CountryCode = "CA",
                PhoneNumber = "+14165550100",
            }, false, DncRegistryConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageDncRegistrySettings_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, canManage: false);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var listsSchema = await source.GetSchemaAsync(DncRegistryReportDataSets.DncLists, context, TestContext.Current.CancellationToken);
        var entriesSchema = await source.GetSchemaAsync(DncRegistryReportDataSets.DncEntries, context, TestContext.Current.CancellationToken);
        var lists = await ReportDesignerTestServices.ReadAsync(source, Query(DncRegistryReportDataSets.DncLists), TestContext.Current.CancellationToken);
        var entries = await ReportDesignerTestServices.ReadAsync(source, Query(DncRegistryReportDataSets.DncEntries), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(listsSchema);
        Assert.Null(entriesSchema);
        Assert.Empty(lists.Rows);
        Assert.Empty(entries.Rows);
    }

    [Fact]
    public async Task WithoutAPrincipal_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = new DataSourceQuery
        {
            DataSet = DncRegistryReportDataSets.DncEntries,
            MaxRows = 100,
            Context = context,
        };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(DncRegistryReportDataSets.DncEntries, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresDatesKeysAndReferences_AndNeverExposesWhereTheFileIsStored()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var lists = await source.GetSchemaAsync(DncRegistryReportDataSets.DncLists, context, TestContext.Current.CancellationToken);
        var entries = await source.GetSchemaAsync(DncRegistryReportDataSets.DncEntries, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DncRegistryReportDataSets.Source, DncRegistryReportDataSource.Name);
        Assert.Equal([DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.DncEntries], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(DncListsReportDataSet.CreatedUtcField, lists.DataSet.DefaultDateField);
        Assert.True(lists.FindField(DncRegistryReportDataSets.ListIdField).IsIdentifier);
        Assert.True(lists.FindField(DncRegistryReportDataSets.ListIdField).IsKeyFilterable);
        Assert.DoesNotContain(lists.Fields, field => field.Name is "StoredFileName" or "ErrorMessages");

        Assert.Null(entries.DataSet.DefaultDateField);
        var list = Assert.Single(entries.FindField(DncRegistryReportDataSets.ListIdField).References);
        Assert.Equal(DncRegistryReportDataSets.Source, list.Source);
        Assert.Equal(DncRegistryReportDataSets.DncLists, list.DataSet);
        Assert.Equal(DncRegistryReportDataSets.ListIdField, list.Field);
        Assert.DoesNotContain(entries.Fields, field => field.Name is "PhoneNumber");
    }

    [Fact]
    public async Task Query_DncLists_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(DncRegistryReportDataSets.DncLists), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["list-2", "list-1"], rows.Select(row => row[DncRegistryReportDataSets.ListIdField]));

        var importing = rows[0];
        Assert.Equal("CA", importing["CountryCode"]);
        Assert.Equal("Processing", importing["Status"]);
        Assert.Null(importing["Name"]);
        Assert.Equal("canada.csv", importing["UploadedFileName"]);
        Assert.Equal(10L, importing["TotalRecords"]);
        Assert.Equal(0L, importing["ErrorCount"]);
        Assert.Equal(1L, importing["FailedAttempts"]);
        Assert.Equal("The upload was interrupted.", importing["Error"]);
        Assert.Null(importing["CompletedUtc"]);

        var imported = rows[1];
        Assert.Equal("Internal opt-outs", imported["Name"]);
        Assert.Equal("Completed", imported["Status"]);
        Assert.Equal(3L, imported["PhoneNumberCount"]);
        Assert.Equal(3L, imported["ImportedCount"]);
        Assert.Equal(1L, imported["ErrorCount"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(imported[DncListsReportDataSet.CreatedUtcField]).Kind);
        Assert.Equal(_start.AddMinutes(5), imported["CompletedUtc"]);
        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && text.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_DncLists_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.ListIdField);
        dates.Conditions.Add(Condition(DncListsReportDataSet.CreatedUtcField, DataFilterOperator.LessThan, _start.AddHours(12)));
        var status = Query(DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.ListIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "processing", "paused"));
        var country = Query(DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.ListIdField);
        country.Conditions.Add(Condition("CountryCode", DataFilterOperator.Equals, "us"));

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var countryRows = Rows(await ReportDesignerTestServices.ReadAsync(source, country, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["list-1"], dateRows.Select(row => row[DncRegistryReportDataSets.ListIdField]));
        Assert.Equal(["list-2"], statusRows.Select(row => row[DncRegistryReportDataSets.ListIdField]));
        Assert.Equal(["list-1"], countryRows.Select(row => row[DncRegistryReportDataSets.ListIdField]));
    }

    [Fact]
    public async Task Query_DncEntries_JoinKeysNeverDropAMatchingRow_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var all = Query(DncRegistryReportDataSets.DncEntries);
        var byList = Query(DncRegistryReportDataSets.DncEntries, DncRegistryReportDataSets.EntryIdField);
        byList.Conditions.Add(new DataCondition { Field = DncRegistryReportDataSets.ListIdField, Operator = DataFilterOperator.In, Values = ["list-2"], IsJoinKey = true });
        var none = Query(DncRegistryReportDataSets.DncEntries, DncRegistryReportDataSets.EntryIdField);
        none.Conditions.Add(new DataCondition { Field = DncRegistryReportDataSets.EntryIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var allRows = Rows(await ReportDesignerTestServices.ReadAsync(source, all, TestContext.Current.CancellationToken));
        var listRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byList, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["entry-4", "entry-3", "entry-2", "entry-1"], allRows.Select(row => row[DncRegistryReportDataSets.EntryIdField]));
        Assert.Equal("list-2", allRows[0][DncRegistryReportDataSets.ListIdField]);
        Assert.Equal("CA", allRows[0]["CountryCode"]);
        Assert.DoesNotContain(allRows.SelectMany(row => row.Values), value => value is string text && text.StartsWith('+'));
        Assert.Equal(["entry-4"], listRows.Select(row => row[DncRegistryReportDataSets.EntryIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_DncEntries_CountsTheNumbersOfEachListInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = DncRegistryReportDataSets.DncEntries,
            Groups = [new DataAggregateGroup { Field = DncRegistryReportDataSets.ListIdField }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => (string)row[0], row => (long)row[1]);
        Assert.Equal(3L, counts["list-1"]);
        Assert.Equal(1L, counts["list-2"]);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var version = await new LocalDncRegistryMigrations(NullLogger<LocalDncRegistryMigrations>.Instance) { SchemaBuilder = schemaBuilder }.UpdateFrom3Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, version);
    }

    [Fact]
    public async Task LocalDncDataSourcesStartup_RegistersTheDataSourceAndBothDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization(canManage: true));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new LocalDncDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var registered = Assert.Single(provider.GetRequiredService<IOptions<DataSourceOptions>>().Value.DataSources);
        var dataSets = await provider.GetRequiredService<DncRegistryReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(typeof(DncRegistryReportDataSource), registered.Type);
        Assert.Equal([DncRegistryReportDataSets.DncLists, DncRegistryReportDataSets.DncEntries], dataSets.Select(dataSet => dataSet.Name));
    }

    private static DncRegistryReportDataSource Source(ISession session, bool canManage = true)
    {
        var authorizationService = Authorization(canManage);

        return new DncRegistryReportDataSource(
            new DncListsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<DncListsReportDataSet>()),
            new DncEntriesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<DncEntriesReportDataSet>()));
    }

    private static IAuthorizationService Authorization(bool canManage)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                canManage && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == DncRegistryPermissions.ManageDncRegistrySettings.Name)
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
