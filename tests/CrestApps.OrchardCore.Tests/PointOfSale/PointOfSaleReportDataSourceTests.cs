using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Migrations;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.PointOfSale;

/// <summary>
/// The point of sale data source reads real registers and shifts from a SQLite store, so the index queries, the fields
/// they expose, the push-down of dates and join keys, the grouping in the database, and the permission they require are
/// all exercised end to end.
/// </summary>
public sealed class PointOfSaleReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pos-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new RegisterIndexProvider(), new RegisterShiftIndexProvider()], PosConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(PosConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new PosMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new Register
            {
                ItemId = "register-1",
                Name = "Front counter",
                LocationId = "location-1",
                Currency = "EUR",
                Address = new Address { AddressLine1 = "1 Market Street", City = "Berlin", Region = "BE", PostalCode = "10115", Country = "DE" },
                Roles = ["Cashier", "Manager"],
                IsEnabled = true,
                CreatedUtc = _start,
                ModifiedUtc = _start,
            }, false, PosConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Register
            {
                ItemId = "register-2",
                Name = "Pop-up stand",
                LocationId = "location-2",
                Currency = "EUR",
                IsEnabled = false,
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
            }, false, PosConstants.CollectionName, cancellationToken);

            // Three shifts, one a day: two closed and counted, and one still open.
            await session.SaveAsync(Shift(0, "register-1", "location-1", closed: true), false, PosConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Shift(1, "register-2", "location-2", closed: true), false, PosConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Shift(2, "register-1", "location-1", closed: false), false, PosConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageRegisters_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [PosPermissions.SellAtRegister.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.PointOfSaleSource.Shifts, context, TestContext.Current.CancellationToken);
        var registers = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.PointOfSaleSource.Registers), TestContext.Current.CancellationToken);
        var shifts = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.PointOfSaleSource.Shifts), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(registers.Rows);
        Assert.Empty(shifts.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.PointOfSaleSource.Shifts);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.PointOfSaleSource.Registers, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesNotes()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var registers = await source.GetSchemaAsync(CommerceReportDataSets.PointOfSaleSource.Registers, context, TestContext.Current.CancellationToken);
        var shifts = await source.GetSchemaAsync(CommerceReportDataSets.PointOfSaleSource.Shifts, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CommerceReportDataSets.PointOfSaleSource.Registers, CommerceReportDataSets.PointOfSaleSource.Shifts], dataSets.Select(dataSet => dataSet.Name));

        Assert.Null(registers.DataSet.DefaultDateField);
        Assert.True(registers.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(registers.FindField("LocationId").IsKeyFilterable);
        var location = Assert.Single(registers.FindField("LocationId").References);
        Assert.Equal(CommerceReportDataSets.InventorySource.Name, location.Source);
        Assert.Equal(CommerceReportDataSets.InventorySource.Locations, location.DataSet);
        Assert.DoesNotContain(registers.Fields, field => field.Name is "AddressLine1" or "Address");

        Assert.Equal(RegisterShiftsReportDataSet.OpenedUtcField, shifts.DataSet.DefaultDateField);
        Assert.True(shifts.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(shifts.FindField("RegisterId").IsKeyFilterable);
        Assert.False(shifts.FindField("OpenedByUserId").IsKeyFilterable);

        var register = Assert.Single(shifts.FindField("RegisterId").References);
        Assert.Equal(CommerceReportDataSets.PointOfSaleSource.Name, register.Source);
        Assert.Equal(CommerceReportDataSets.PointOfSaleSource.Registers, register.DataSet);
        Assert.Equal(CommerceReportDataSets.ItemIdField, register.Field);

        var staff = Assert.Single(shifts.FindField("OpenedByUserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, staff.Source);

        Assert.Equal(DataFieldType.Decimal, shifts.FindField("CountedCash").Type);
        Assert.False(string.IsNullOrEmpty(shifts.FindField("CountedCash").Description));
        Assert.NotNull(shifts.FindField("Currency"));
        Assert.DoesNotContain(shifts.Fields, field => field.Name is "CloseNote" or "CashMovements" or "Reason");
    }

    [Fact]
    public async Task Query_Registers_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.PointOfSaleSource.Registers), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["register-2", "register-1"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var counter = rows[1];
        Assert.Equal("Front counter", counter["Name"]);
        Assert.Equal(true, counter["IsEnabled"]);
        Assert.Equal("location-1", counter["LocationId"]);
        Assert.Equal("EUR", counter["Currency"]);
        Assert.Equal("Cashier, Manager", counter["Roles"]);
        Assert.Equal("DE", counter["Country"]);
        Assert.Equal("Berlin", counter["City"]);
        Assert.Equal("10115", counter["PostalCode"]);

        var popUp = rows[0];
        Assert.Equal(false, popUp["IsEnabled"]);
        Assert.Null(popUp["Roles"]);
        Assert.Null(popUp["Country"]);
    }

    [Fact]
    public async Task Query_Shifts_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.PointOfSaleSource.Shifts), TestContext.Current.CancellationToken);
        var rows = Rows(table);

        // Assert
        Assert.Equal(["shift-2", "shift-1", "shift-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var closed = rows[2];
        Assert.Equal("Closed", closed["Status"]);
        Assert.Equal("register-1", closed["RegisterId"]);
        Assert.Equal("Front counter", closed["RegisterName"]);
        Assert.Equal("EUR", closed["Currency"]);
        Assert.Equal("staff-1", closed["OpenedByUserId"]);
        Assert.Equal("Sam", closed["OpenedByName"]);
        Assert.Equal("staff-2", closed["ClosedByUserId"]);
        Assert.Equal(_start, closed["OpenedUtc"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(closed["ClosedUtc"]).Kind);
        Assert.Equal(100m, closed["OpeningFloat"]);
        Assert.Equal(2L, closed["CashMovementCount"]);
        Assert.Equal(20m, closed["CashIn"]);
        Assert.Equal(15m, closed["CashOut"]);
        Assert.Equal(150m, closed["ExpectedCash"]);
        Assert.Equal(145m, closed["CountedCash"]);
        Assert.Equal(-5m, closed["CashDifference"]);
        Assert.Equal(4L, closed["SaleCount"]);
        Assert.Equal(120m, closed["SalesTotal"]);
        Assert.Equal(45m, closed["CashSales"]);
        Assert.Equal(60m, closed["CardSales"]);
        Assert.Equal(15m, closed["OtherSales"]);
        Assert.Equal(10m, closed["RefundedAmount"]);

        var open = rows[0];
        Assert.Equal("Open", open["Status"]);
        Assert.Null(open["ClosedUtc"]);
        Assert.Null(open["ExpectedCash"]);
        Assert.Null(open["CountedCash"]);
        Assert.Null(open["CashDifference"]);
        Assert.Null(open["SaleCount"]);
        Assert.Null(open["SalesTotal"]);

        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_Shifts_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.PointOfSaleSource.Shifts, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(RegisterShiftsReportDataSet.OpenedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.PointOfSaleSource.Shifts, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "RegisterId", Operator = DataFilterOperator.In, Values = ["register-1"], IsJoinKey = true });
        var names = Query(CommerceReportDataSets.PointOfSaleSource.Registers, CommerceReportDataSets.ItemIdField);
        names.Conditions.Add(Condition("Name", DataFilterOperator.Equals, "front COUNTER"));
        var none = Query(CommerceReportDataSets.PointOfSaleSource.Shifts, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var nameRows = Rows(await ReportDesignerTestServices.ReadAsync(source, names, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["shift-2", "shift-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["shift-2", "shift-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["register-1"], nameRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsShiftsByRegister_AndDeclinesTheStatus()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byRegister = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.PointOfSaleSource.Shifts,
            Groups = [new DataAggregateGroup { Field = "RegisterId" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
        var byStatus = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.PointOfSaleSource.Shifts,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var registers = await ((IDataAggregateSource)source).AggregateAsync(byRegister, TestContext.Current.CancellationToken);
        var statuses = await ((IDataAggregateSource)source).AggregateAsync(byStatus, TestContext.Current.CancellationToken);

        // Assert
        // The database groups text by its own collation, so a group by the register is left to the report; a filter on
        // it is still applied in the database.
        Assert.Null(registers);

        foreach (var (register, count) in new[] { ("register-1", 2L), ("REGISTER-2", 1L) })
        {
            var groups = await ((IDataAggregateSource)source).AggregateAsync(new DataAggregateQuery
            {
                DataSet = CommerceReportDataSets.PointOfSaleSource.Shifts,
                Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
                Conditions = [new DataCondition { Field = "RegisterId", Operator = DataFilterOperator.Equals, Values = [register] }],
                Context = new DataSourceContext { User = _user },
            }, TestContext.Current.CancellationToken);

            Assert.NotNull(groups);
            Assert.Equal(count, Convert.ToInt64(Assert.Single(groups)[0], CultureInfo.InvariantCulture));
        }

        // The index keeps the status as a number, so the grouping is left to the report.
        Assert.Null(statuses);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var first = await new PosMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var second = await new PosMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

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
        services.AddSingleton(Authorization([PosPermissions.ManageRegisters.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<PointOfSaleReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.PointOfSaleSource.Name, PointOfSaleReportDataSource.Name);
        Assert.Equal([CommerceReportDataSets.PointOfSaleSource.Registers, CommerceReportDataSets.PointOfSaleSource.Shifts], dataSets.Select(dataSet => dataSet.Name));
    }

    private static RegisterShift Shift(int day, string registerId, string locationId, bool closed)
    {
        var opened = _start.AddDays(day);
        var shift = new RegisterShift
        {
            ItemId = $"shift-{day}",
            RegisterId = registerId,
            RegisterName = registerId == "register-1" ? "Front counter" : "Pop-up stand",
            LocationId = locationId,
            Currency = "EUR",
            Status = closed ? RegisterShiftStatus.Closed : RegisterShiftStatus.Open,
            OpenedByUserId = "staff-1",
            OpenedByName = "Sam",
            OpenedUtc = opened,
            OpeningFloat = 100m,
            CashMovements =
            [
                new RegisterCashMovement { Amount = 20m, Reason = "Private change top-up", ByUserId = "staff-1", ByName = "Sam", Utc = opened.AddHours(1) },
                new RegisterCashMovement { Amount = -15m, Reason = "Private refund", ByUserId = "staff-1", ByName = "Sam", Utc = opened.AddHours(2) },
            ],
        };

        if (closed)
        {
            shift.ClosedByUserId = "staff-2";
            shift.ClosedByName = "Alex";
            shift.ClosedUtc = opened.AddHours(8);
            shift.Summary = new RegisterShiftSummary { Sales = 4, Total = 120m, Cash = 45m, Card = 60m, Other = 15m, Refunded = 10m };
            shift.ExpectedCash = shift.GetExpectedCash(45m);
            shift.CountedCash = 145m;
            shift.CloseNote = "Private note about the drawer";
        }

        return shift;
    }

    private static PointOfSaleReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [PosPermissions.ManageRegisters.Name]);

        return new PointOfSaleReportDataSource(
        [
            new RegistersReportDataSet(session, authorizationService, new PassThroughStringLocalizer<RegistersReportDataSet>()),
            new RegisterShiftsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<RegisterShiftsReportDataSet>()),
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
