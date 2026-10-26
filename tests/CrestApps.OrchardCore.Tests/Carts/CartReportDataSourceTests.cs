using System.Security.Claims;
using CrestApps.OrchardCore.Carts;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Indexes;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Migrations;
using CrestApps.OrchardCore.Carts.Reports.DataSources;
using CrestApps.OrchardCore.Carts.Wishlists;
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
using OrderPermissions = CrestApps.OrchardCore.Orders.OrderPermissions;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// The carts data source reads real carts and wishlists from a SQLite store, so the index queries, the fields they
/// expose, the push-down of join keys, the grouping in the database, and the permission they require are all exercised
/// end to end.
/// </summary>
public sealed class CartReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"cart-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CartIndexProvider(), new WishlistIndexProvider()], CartConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(CartConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new CartMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
            await new WishlistMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new Cart
            {
                ItemId = "cart-0",
                UserId = "user-1",
                Currency = "USD",
                Lines =
                [
                    new CartLine { LineId = "line-a", ContentItemId = "product-mug", PriceId = "price-1", Quantity = 2, AddedUnitPrice = 20m, AddedUtc = _start },
                    new CartLine { LineId = "line-b", ContentItemId = "product-cap", VariantId = "variant-red", Quantity = 1, AddedUnitPrice = 60m, AddedUtc = _start.AddHours(1) },
                ],
                CreatedUtc = _start,
                ModifiedUtc = _start.AddHours(1),
                ExpiresUtc = _start.AddDays(90),
            }, false, CartConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Cart
            {
                ItemId = "cart-1",
                GuestTokenHash = "secret-guest-token-hash",
                Currency = "EUR",
                Lines =
                [
                    new CartLine
                    {
                        LineId = "line-c",
                        ContentItemId = "product-mug",
                        Quantity = 3,
                        AddedUnitPrice = 18m,
                        AddOns = new Dictionary<string, string> { ["engraving"] = "Private engraving" },
                        Details = new Dictionary<string, string> { ["note"] = "Private detail" },
                        AddedUtc = _start.AddDays(1),
                    },
                ],
                CreatedUtc = _start.AddDays(1),
                ModifiedUtc = _start.AddDays(1),
                ExpiresUtc = _start.AddDays(31),
            }, false, CartConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Cart
            {
                ItemId = "cart-2",
                UserId = "user-2",
                Currency = "USD",
                CreatedUtc = _start.AddDays(2),
                ModifiedUtc = _start.AddDays(2),
                ExpiresUtc = _start.AddDays(92),
            }, false, CartConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Wishlist
            {
                ItemId = "wishlist-1",
                UserId = "user-1",
                Items =
                [
                    new WishlistItem { ContentItemId = "product-cap", AddedUtc = _start.AddDays(1) },
                    new WishlistItem { ContentItemId = "product-mug", AddedUtc = _start },
                ],
                ModifiedUtc = _start.AddDays(1),
            }, false, CartConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new Wishlist
            {
                ItemId = "wishlist-2",
                UserId = "user-2",
                Items = [new WishlistItem { ContentItemId = "product-hat", AddedUtc = _start.AddDays(2) }],
                ModifiedUtc = _start.AddDays(2),
            }, false, CartConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageOrders_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: []);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.Carts, context, TestContext.Current.CancellationToken);
        var carts = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.Carts), TestContext.Current.CancellationToken);
        var lines = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.CartLines), TestContext.Current.CancellationToken);
        var wishlists = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.WishlistItems), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(carts.Rows);
        Assert.Empty(lines.Rows);
        Assert.Empty(wishlists.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.Carts, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var carts = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.Carts, context, TestContext.Current.CancellationToken);
        var lines = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.CartLines, context, TestContext.Current.CancellationToken);
        var wishlists = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.Wishlists, context, TestContext.Current.CancellationToken);
        var items = await source.GetSchemaAsync(CommerceReportDataSets.CartSource.WishlistItems, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OrderPermissions.ManageOrders.Name, CartReportPermissions.ManageOrders.Name);
        Assert.Equal(
            [
                CommerceReportDataSets.CartSource.Carts,
                CommerceReportDataSets.CartSource.CartLines,
                CommerceReportDataSets.CartSource.Wishlists,
                CommerceReportDataSets.CartSource.WishlistItems,
            ],
            dataSets.Select(dataSet => dataSet.Name));

        // No index of carts or wishlists holds a date, so none of their data sets has a default date to push down.
        Assert.All([carts, lines, wishlists, items], schema => Assert.Null(schema.DataSet.DefaultDateField));
        Assert.True(carts.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(carts.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(carts.FindField("UserId").IsKeyFilterable);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(carts.FindField("UserId").References).Source);
        Assert.False(string.IsNullOrEmpty(carts.FindField("Value").Description));
        Assert.DoesNotContain(carts.Fields, field => field.Name is "GuestTokenHash" or "Lines");

        Assert.True(lines.FindField(CartLinesReportDataSet.CartIdField).IsKeyFilterable);
        var cart = Assert.Single(lines.FindField(CartLinesReportDataSet.CartIdField).References);
        Assert.Equal(CommerceReportDataSets.CartSource.Name, cart.Source);
        Assert.Equal(CommerceReportDataSets.CartSource.Carts, cart.DataSet);
        var product = Assert.Single(lines.FindField("ProductId").References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);
        var variant = Assert.Single(lines.FindField("VariantId").References);
        Assert.Equal(CommerceReportDataSets.VariantIdField, variant.Field);
        Assert.DoesNotContain(lines.Fields, field => field.Name is "AddOns" or "Details" or "GuestTokenHash");

        Assert.True(items.FindField(WishlistItemsReportDataSet.WishlistIdField).IsKeyFilterable);
        Assert.Equal(CommerceReportDataSets.CartSource.Wishlists, Assert.Single(items.FindField(WishlistItemsReportDataSet.WishlistIdField).References).DataSet);
    }

    [Fact]
    public async Task Query_Carts_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.CartSource.Carts, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(CartsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var users = Query(CommerceReportDataSets.CartSource.Carts, CommerceReportDataSets.ItemIdField);
        users.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });
        var none = Query(CommerceReportDataSets.CartSource.Carts, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.Carts), TestContext.Current.CancellationToken));
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, users, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["cart-2", "cart-1", "cart-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var signedIn = rows[2];
        Assert.Equal("user-1", signedIn["UserId"]);
        Assert.Equal(false, signedIn["IsGuest"]);
        Assert.Equal("USD", signedIn["Currency"]);
        Assert.Equal(2L, signedIn["LineCount"]);
        Assert.Equal(3L, signedIn["ItemCount"]);
        Assert.Equal(100m, signedIn["Value"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(signedIn["ExpiresUtc"]).Kind);

        var guest = rows[1];
        Assert.Null(guest["UserId"]);
        Assert.Equal(true, guest["IsGuest"]);
        Assert.Equal(54m, guest["Value"]);
        Assert.DoesNotContain(guest.Values, value => value is string text && text.Contains("secret", StringComparison.Ordinal));

        var empty = rows[0];
        Assert.Equal(0L, empty["LineCount"]);
        Assert.Equal(0m, empty["Value"]);

        // The creation date is not in the index: the range keeps every cart for the consumer to filter.
        Assert.Contains(dateRows, row => (string)row[CommerceReportDataSets.ItemIdField] == "cart-1");
        Assert.Contains(dateRows, row => (string)row[CommerceReportDataSets.ItemIdField] == "cart-2");
        Assert.Equal(["cart-0"], userRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Query_CartLines_GivesOneRowPerLineWithItsCart()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = Query(CommerceReportDataSets.CartSource.CartLines);
        query.Conditions.Add(new DataCondition { Field = CartLinesReportDataSet.CartIdField, Operator = DataFilterOperator.In, Values = ["cart-0"], IsJoinKey = true });

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.CartLines), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["line-a", "line-b"], rows.Select(row => row["LineId"]));
        Assert.Equal([1L, 2L], rows.Select(row => row["Position"]));
        Assert.All(rows, row => Assert.Equal("cart-0", row[CartLinesReportDataSet.CartIdField]));
        Assert.All(rows, row => Assert.Equal("user-1", row["UserId"]));
        Assert.Equal("product-mug", rows[0]["ProductId"]);
        Assert.Equal("price-1", rows[0]["PriceId"]);
        Assert.Equal(2L, rows[0]["Quantity"]);
        Assert.Equal(20m, rows[0]["AddedUnitPrice"]);
        Assert.Equal(40m, rows[0]["Value"]);
        Assert.Equal("variant-red", rows[1]["VariantId"]);

        Assert.Equal(["line-c", "line-a", "line-b"], all.Select(row => row["LineId"]));
        Assert.All(all, row => Assert.DoesNotContain(row.Values, value => value is string text && text.Contains("Private", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Aggregate_Carts_CountsInTheDatabase_AndTheLinesDecline()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var count = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CartSource.Carts,
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var byCurrency = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CartSource.Carts,
            Groups = [new DataAggregateGroup { Field = "Currency" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var lines = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CartSource.CartLines,
            Groups = [new DataAggregateGroup { Field = "UserId" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        var unindexed = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CartSource.Carts,
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        unindexed.Conditions.Add(Condition("Value", DataFilterOperator.GreaterThan, 50m));

        var userCount = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.CartSource.Carts,
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        userCount.Conditions.Add(Condition("UserId", DataFilterOperator.In, "user-1", "user-2"));

        // Act
        var total = await ((IDataAggregateSource)source).AggregateAsync(count, TestContext.Current.CancellationToken);
        var currencies = await ((IDataAggregateSource)source).AggregateAsync(byCurrency, TestContext.Current.CancellationToken);
        var lineGroups = await ((IDataAggregateSource)source).AggregateAsync(lines, TestContext.Current.CancellationToken);
        var filtered = await ((IDataAggregateSource)source).AggregateAsync(unindexed, TestContext.Current.CancellationToken);
        var signedIn = await ((IDataAggregateSource)source).AggregateAsync(userCount, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(total);
        Assert.Equal(3L, (long)Assert.Single(total)[0]);
        Assert.Null(currencies);
        Assert.Null(lineGroups);

        // A condition on a field the index doesn't hold can't be answered in the database; one it holds can.
        Assert.Null(filtered);
        Assert.NotNull(signedIn);
        Assert.Equal(2L, (long)Assert.Single(signedIn)[0]);
    }

    [Fact]
    public async Task Query_Wishlists_AndTheirItems()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byUser = Query(CommerceReportDataSets.CartSource.WishlistItems);
        byUser.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });

        // Act
        var wishlists = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.CartSource.Wishlists), TestContext.Current.CancellationToken));
        var items = Rows(await ReportDesignerTestServices.ReadAsync(source, byUser, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["wishlist-2", "wishlist-1"], wishlists.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(2L, wishlists[1]["ItemCount"]);
        Assert.Equal("user-1", wishlists[1]["UserId"]);

        Assert.Equal(["product-cap", "product-mug"], items.Select(row => row["ProductId"]));
        Assert.Equal([1L, 2L], items.Select(row => row["Position"]));
        Assert.All(items, row => Assert.Equal("wishlist-1", row[WishlistItemsReportDataSet.WishlistIdField]));
        Assert.Equal(_start.AddDays(1), items[0][WishlistItemsReportDataSet.AddedUtcField]);
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheWishlistsOnlyWithTheirFeature()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var carts = Services(session);
        var withWishlists = Services(session);

        // Act
        new DataSourcesStartup().ConfigureServices(carts);
        new DataSourcesStartup().ConfigureServices(withWishlists);
        new WishlistsDataSourcesStartup().ConfigureServices(withWishlists);

        await using var cartsProvider = carts.BuildServiceProvider();
        await using var wishlistsProvider = withWishlists.BuildServiceProvider();
        var cartDataSets = await cartsProvider.GetRequiredService<CartReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);
        var allDataSets = await wishlistsProvider.GetRequiredService<CartReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.CartSource.Name, CartReportDataSource.Name);
        Assert.Equal([CommerceReportDataSets.CartSource.Carts, CommerceReportDataSets.CartSource.CartLines], cartDataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(4, allDataSets.Count);
    }

    private static ServiceCollection Services(ISession session)
    {
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([OrderPermissions.ManageOrders.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        return services;
    }

    private static CartReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [OrderPermissions.ManageOrders.Name]);

        return new CartReportDataSource(
        [
            new CartsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CartsReportDataSet>()),
            new CartLinesReportDataSet(session, authorizationService, new PassThroughStringLocalizer<CartLinesReportDataSet>()),
            new WishlistsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<WishlistsReportDataSet>()),
            new WishlistItemsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<WishlistItemsReportDataSet>()),
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
