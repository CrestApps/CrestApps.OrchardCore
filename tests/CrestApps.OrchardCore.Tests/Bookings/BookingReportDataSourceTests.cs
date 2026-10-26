using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Migrations;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Reports.DataSources;
using CrestApps.OrchardCore.Reports;
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
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// The bookings data source reads real bookings, booking dates and balances from a SQLite store, so the index queries,
/// the fields they expose, the push-down of dates, join keys and statuses, the grouping in the database, and the
/// permission they require are all exercised end to end.
/// </summary>
public sealed class BookingReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"booking-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new BookingSlotIndexProvider(), new BookingIndexProvider(), new BookingBalanceIndexProvider()], BookingsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(BookingsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await Migrations(schemaBuilder).CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Three dates: two departures of a tour, one full and closed, and a night of a room.
            await session.SaveAsync(Slot(0, "product-tour", capacity: 10, held: 2, sold: 5, closed: false, price: 99m), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Slot(1, "product-tour", capacity: 10, held: 0, sold: 10, closed: true, price: null), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Slot(2, "product-room", capacity: 2, held: 0, sold: 1, closed: false, price: null), false, BookingsConstants.CollectionName, cancellationToken);

            // Four bookings, one a day: confirmed, canceled, requested and held.
            await session.SaveAsync(Booking(0, "order-1", "product-tour", BookingStatus.Confirmed), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Booking(1, "order-2", "product-room", BookingStatus.Canceled), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Booking(2, "order-2", "product-tour", BookingStatus.Requested), false, BookingsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Booking(3, "order-3", "product-tour", BookingStatus.Held), false, BookingsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new BookingBalance
            {
                ItemId = "balance-0",
                OrderId = "order-1",
                OrderNumber = "N-1",
                TransactionId = "transaction-1",
                DepositSessionId = "checkout-1",
                OwnerId = "user-1",
                CustomerName = "Ada",
                CustomerEmail = "ada@example.com",
                Amount = 150m,
                Currency = "USD",
                DueUtc = _start.AddDays(8),
                Status = BookingBalanceStatus.Scheduled,
                CreatedUtc = _start,
            }, false, BookingsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new BookingBalance
            {
                ItemId = "balance-1",
                OrderId = "order-2",
                OrderNumber = "N-2",
                Amount = 80m,
                Currency = "USD",
                DueUtc = _start.AddDays(5),
                Status = BookingBalanceStatus.Failed,
                Attempts = 3,
                LastAttemptUtc = _start.AddDays(6),
                LastFailure = "Private card decline",
                CreatedUtc = _start.AddDays(1),
            }, false, BookingsConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageBookings_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.BookingSource.Bookings, context, TestContext.Current.CancellationToken);
        var bookings = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Bookings), TestContext.Current.CancellationToken);
        var slots = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Slots), TestContext.Current.CancellationToken);
        var balances = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Balances), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(bookings.Rows);
        Assert.Empty(slots.Rows);
        Assert.Empty(balances.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.BookingSource.Bookings);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.BookingSource.Slots, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesTravelersOrPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var bookings = await source.GetSchemaAsync(CommerceReportDataSets.BookingSource.Bookings, context, TestContext.Current.CancellationToken);
        var slots = await source.GetSchemaAsync(CommerceReportDataSets.BookingSource.Slots, context, TestContext.Current.CancellationToken);
        var balances = await source.GetSchemaAsync(CommerceReportDataSets.BookingSource.Balances, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.BookingSource.Bookings,
                CommerceReportDataSets.BookingSource.Slots,
                CommerceReportDataSets.BookingSource.Balances,
            ],
            dataSets.Select(dataSet => dataSet.Name));

        Assert.Equal(BookingsReportDataSet.CreatedUtcField, bookings.DataSet.DefaultDateField);
        Assert.True(bookings.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(bookings.FindField("OrderId").IsKeyFilterable);
        Assert.True(bookings.FindField(BookingsReportDataSet.ProductIdField).IsKeyFilterable);
        Assert.False(bookings.FindField("UserId").IsKeyFilterable);
        var product = Assert.Single(bookings.FindField(BookingsReportDataSet.ProductIdField).References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);
        var user = Assert.Single(bookings.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.False(string.IsNullOrEmpty(bookings.FindField("Total").Description));
        Assert.NotNull(bookings.FindField("Currency"));
        Assert.DoesNotContain(bookings.Fields, field => field.Name is "Travelers" or "CancelReason" or "CancellationTiers" or "SlotKeys");

        Assert.Equal(BookingSlotsReportDataSet.StartsUtcField, slots.DataSet.DefaultDateField);
        Assert.True(slots.FindField(BookingSlotsReportDataSet.ProductIdField).IsKeyFilterable);
        Assert.DoesNotContain(slots.Fields, field => field.Name is "Price");

        Assert.Equal(BookingBalancesReportDataSet.DueUtcField, balances.DataSet.DefaultDateField);
        Assert.True(balances.FindField("OrderId").IsKeyFilterable);
        var transaction = Assert.Single(balances.FindField("TransactionId").References);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Name, transaction.Source);
        Assert.Equal(CommerceReportDataSets.TransactionSource.Transactions, transaction.DataSet);
        Assert.DoesNotContain(balances.Fields, field => field.Name is "LastFailure" or "OwnerId");
    }

    [Fact]
    public async Task Query_Bookings_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Bookings), TestContext.Current.CancellationToken);
        var rows = Rows(table);

        // Assert
        Assert.Equal(["booking-3", "booking-2", "booking-1", "booking-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["Held", "Requested", "Canceled", "Confirmed"], rows.Select(row => row["Status"]));

        var confirmed = rows[3];
        Assert.Equal("Departure", confirmed["Kind"]);
        Assert.Equal("product-tour", confirmed[BookingsReportDataSet.ProductIdField]);
        Assert.Equal("Tour", confirmed["Title"]);
        Assert.Equal(2L, confirmed["People"]);
        Assert.Equal(2L, confirmed["Seats"]);
        Assert.Equal(1L, confirmed["Units"]);
        Assert.Equal(1L, confirmed["DateCount"]);
        Assert.Equal(1L, confirmed["TravelerCount"]);
        Assert.Equal("order-1", confirmed["OrderId"]);
        Assert.Equal("line-0", confirmed["LineId"]);
        Assert.Equal("user-1", confirmed["UserId"]);
        Assert.Equal("Ada", confirmed["CustomerName"]);
        Assert.Equal("USD", confirmed["Currency"]);
        Assert.Equal(198m, confirmed["Total"]);
        Assert.Equal(_start, confirmed["CreatedUtc"]);
        Assert.Equal(_start.AddDays(10), confirmed["StartsUtc"]);
        Assert.Null(confirmed["CancellationFee"]);

        var canceled = rows[2];
        Assert.Equal("Nightly", canceled["Kind"]);
        Assert.Equal(2L, canceled["DateCount"]);
        Assert.Equal(50m, canceled["CancellationFee"]);
        Assert.Equal(148m, canceled["Refunded"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(canceled["CanceledUtc"]).Kind);

        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_SlotsAndBalances_ReturnTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var slots = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Slots), TestContext.Current.CancellationToken));
        var balanceTable = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.BookingSource.Balances), TestContext.Current.CancellationToken);
        var balances = Rows(balanceTable);

        // Assert
        Assert.Equal(["slot-2", "slot-1", "slot-0"], slots.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var departure = slots[2];
        Assert.Equal("product-tour", departure[BookingSlotsReportDataSet.ProductIdField]);
        Assert.Equal("2026-09-11", departure["SlotKey"]);
        Assert.Equal(_start.AddDays(10), departure["StartsUtc"]);
        Assert.Equal(10L, departure["Capacity"]);
        Assert.Equal(2L, departure["Held"]);
        Assert.Equal(5L, departure["Sold"]);
        Assert.Equal(3L, departure["Remaining"]);
        Assert.Equal(false, departure["Closed"]);
        Assert.Equal(true, departure["HasOwnPrice"]);
        Assert.Equal(true, slots[1]["Closed"]);
        Assert.Equal(0L, slots[1]["Remaining"]);
        Assert.Equal(false, slots[1]["HasOwnPrice"]);

        Assert.Equal(["balance-0", "balance-1"], balances.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var scheduled = balances[0];
        Assert.Equal("Scheduled", scheduled["Status"]);
        Assert.Equal(150m, scheduled["Amount"]);
        Assert.Equal("USD", scheduled["Currency"]);
        Assert.Equal("order-1", scheduled["OrderId"]);
        Assert.Equal("transaction-1", scheduled["TransactionId"]);
        Assert.Equal("checkout-1", scheduled["DepositSessionId"]);
        Assert.Equal("user-1", scheduled["UserId"]);
        Assert.Equal(_start.AddDays(8), scheduled["DueUtc"]);

        var failed = balances[1];
        Assert.Equal("Failed", failed["Status"]);
        Assert.Equal(3L, failed["Attempts"]);
        Assert.Null(failed["UserId"]);
        Assert.DoesNotContain(balanceTable.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.BookingSource.Bookings, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(BookingsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var products = Query(CommerceReportDataSets.BookingSource.Bookings, CommerceReportDataSets.ItemIdField);
        products.Conditions.Add(new DataCondition { Field = BookingsReportDataSet.ProductIdField, Operator = DataFilterOperator.In, Values = ["product-tour"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.BookingSource.Bookings, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "confirmed", "requested"));
        var slotDates = Query(CommerceReportDataSets.BookingSource.Slots, CommerceReportDataSets.ItemIdField);
        slotDates.Conditions.Add(Condition(BookingSlotsReportDataSet.StartsUtcField, DataFilterOperator.Between, _start.AddDays(11), _start.AddDays(12)));
        var slotProducts = Query(CommerceReportDataSets.BookingSource.Slots, CommerceReportDataSets.ItemIdField);
        slotProducts.Conditions.Add(new DataCondition { Field = BookingSlotsReportDataSet.ProductIdField, Operator = DataFilterOperator.In, Values = ["product-tour"], IsJoinKey = true });
        var balances = Query(CommerceReportDataSets.BookingSource.Balances, CommerceReportDataSets.ItemIdField);
        balances.Conditions.Add(new DataCondition { Field = "OrderId", Operator = DataFilterOperator.In, Values = ["order-2"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.BookingSource.Bookings, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var productRows = Rows(await ReportDesignerTestServices.ReadAsync(source, products, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var slotDateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, slotDates, TestContext.Current.CancellationToken));
        var slotProductRows = Rows(await ReportDesignerTestServices.ReadAsync(source, slotProducts, TestContext.Current.CancellationToken));
        var balanceRows = Rows(await ReportDesignerTestServices.ReadAsync(source, balances, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["booking-2", "booking-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["booking-3", "booking-2", "booking-0"], productRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["booking-2", "booking-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["slot-2", "slot-1"], slotDateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["slot-1", "slot-0"], slotProductRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["balance-1"], balanceRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsBookingsAndSumsWhatIsLeftByProduct_AndDeclinesTheTitle()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var bookingsByProduct = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.BookingSource.Bookings,
            Groups = [new DataAggregateGroup { Field = BookingsReportDataSet.ProductIdField }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
        var leftByProduct = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.BookingSource.Slots,
            Groups = [new DataAggregateGroup { Field = BookingSlotsReportDataSet.ProductIdField }],
            Measures = [new DataAggregateMeasure { Field = "Remaining", Function = DataAggregate.Sum }],
            Context = new DataSourceContext { User = _user },
        };
        var byTitle = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.BookingSource.Bookings,
            Groups = [new DataAggregateGroup { Field = "Title" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var bookings = await ((IDataAggregateSource)source).AggregateAsync(bookingsByProduct, TestContext.Current.CancellationToken);
        var left = await ((IDataAggregateSource)source).AggregateAsync(leftByProduct, TestContext.Current.CancellationToken);
        var titles = await ((IDataAggregateSource)source).AggregateAsync(byTitle, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(bookings);
        var counts = bookings.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(3L, counts["product-tour"]);
        Assert.Equal(1L, counts["product-room"]);

        Assert.NotNull(left);
        var remaining = left.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(3L, remaining["product-tour"]);
        Assert.Equal(1L, remaining["product-room"]);

        // The index keeps a shortened title, so the grouping is left to the report.
        Assert.Null(titles);
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
        services.AddSingleton(Authorization([BookingsPermissions.ManageBookings.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<BookingReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.BookingSource.Name, BookingReportDataSource.Name);
        Assert.Equal(3, dataSets.Count);
    }

    private static BookingSlot Slot(int index, string productId, int capacity, int held, int sold, bool closed, decimal? price)
    {
        var starts = _start.AddDays(10 + index);

        return new BookingSlot
        {
            ItemId = $"slot-{index}",
            ContentItemId = productId,
            SlotKey = starts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StartsUtc = starts,
            Capacity = capacity,
            Held = held,
            Sold = sold,
            Closed = closed,
            Price = price,
            ModifiedUtc = _start,
        };
    }

    private static Booking Booking(int day, string orderId, string productId, BookingStatus status)
    {
        var booked = _start.AddDays(day);
        var nightly = productId == "product-room";
        var booking = new Booking
        {
            ItemId = $"booking-{day}",
            OrderId = orderId,
            OrderNumber = "N-" + orderId[^1..],
            LineId = $"line-{day}",
            ContentItemId = productId,
            Title = nightly ? "Room" : "Tour",
            Kind = nightly ? BookingKind.Nightly : BookingKind.Departure,
            SlotKeys = nightly ? ["2026-09-13", "2026-09-14"] : ["2026-09-11"],
            StartsUtc = _start.AddDays(10),
            EndsUtc = nightly ? _start.AddDays(12) : null,
            Units = 1,
            People = 2,
            Seats = 2,
            Status = status,
            HoldExpiresUtc = status == BookingStatus.Held ? booked.AddHours(1) : null,
            UserId = "user-1",
            CustomerName = "Ada",
            CustomerEmail = "ada@example.com",
            Total = 198m,
            Currency = "USD",
            Travelers = [new JsonObject { ["Passport"] = "Private passport number" }],
            CreatedUtc = booked,
            ModifiedUtc = booked,
            ConfirmedUtc = status == BookingStatus.Confirmed ? booked.AddHours(1) : null,
        };

        if (status == BookingStatus.Canceled)
        {
            booking.CancellationFee = 50m;
            booking.Refunded = 148m;
            booking.CancelReason = "Private reason for canceling";
            booking.CanceledUtc = booked.AddDays(1);
        }

        return booking;
    }

    private static BookingMigrations Migrations(ISchemaBuilder schemaBuilder)
    {
        return new BookingMigrations(new Mock<IContentDefinitionManager>().Object)
        {
            SchemaBuilder = schemaBuilder,
        };
    }

    private static BookingReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [BookingsPermissions.ManageBookings.Name]);

        return new BookingReportDataSource(
        [
            new BookingsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<BookingsReportDataSet>()),
            new BookingSlotsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<BookingSlotsReportDataSet>()),
            new BookingBalancesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<BookingBalancesReportDataSet>()),
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
