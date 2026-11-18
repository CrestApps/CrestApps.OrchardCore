using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The deferred task of orders migration 5, which indexes the unpaid orders again, while live traffic saves the same
/// orders. The orders are in a real SQLite store; a second session saves an order while the backfill is saving its page,
/// after the backfill read it and before its page commits.
/// </summary>
public sealed class OrderUnpaidBackfillTests : IAsyncLifetime
{
    private static readonly DateTime _heldUtc = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-order-backfill-{Guid.NewGuid():N}.db");
    private readonly ConcurrentSaveIndexProvider _hook = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new OrderIndexProvider(), _hook], OrderConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OrderConstants.CollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await new OrderMigrations(steps: null) { SchemaBuilder = schemaBuilder }.CreateAsync();
        await schemaBuilder.CreateMapIndexTableAsync<ConcurrentSaveIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26)),
            collection: OrderConstants.CollectionName);
        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// K2: the backfill saved its copy of each order without a version check, so an order a payment webhook saved as
    /// Paid after the backfill read it went back to Awaiting payment. The order now stays Paid, in its document and its
    /// index.
    /// </summary>
    [Fact]
    public async Task AnOrderPaidAfterTheBackfillReadIt_StaysPaid()
    {
        // Arrange
        await AddAsync("order-1", OrderStatus.AwaitingPayment);
        await AddAsync("order-2", OrderStatus.AwaitingPayment);

        // The webhook saves order-1 as Paid while the backfill saves the page it read.
        _hook.Arm("order-1", () => PayAsync("order-1"));

        // Act
        await using (var session = _store.CreateSession())
        {
            await OrderMigrations.ReindexUnpaidAsync(session, NullLogger.Instance);
        }

        // Assert
        Assert.True(_hook.Fired);

        await using var check = _store.CreateSession();
        var order = await new OrderStore(check).FindByIdAsync("order-1", TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(_heldUtc.AddDays(1), order.PaidUtc);
        Assert.Equal(OrderStatus.Paid, (await check.QueryIndex<OrderIndex>(index => index.ItemId == "order-1", OrderConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken)).Status);
    }

    /// <summary>
    /// K7: the backfill paged with <c>Skip</c> over the unpaid orders, so an order paid while a page was saved moved the
    /// next page forward by one, and the first order of that page was never indexed again. It now pages by key, so every
    /// order still unpaid is indexed.
    /// </summary>
    [Fact]
    public async Task AnOrderPaidBetweenPages_DoesNotMakeTheBackfillSkipTheNextOne()
    {
        // Arrange
        string[] itemIds = ["order-1", "order-2", "order-3", "order-4", "order-5"];

        foreach (var itemId in itemIds)
        {
            await AddAsync(itemId, OrderStatus.AwaitingPayment);
        }

        await ClearBudgetColumnAsync();

        // order-1 is paid while the first page (order-1 and order-2) is saved, so it leaves the unpaid orders.
        _hook.Arm("order-2", () => PayAsync("order-1"));

        // Act
        await using (var session = _store.CreateSession())
        {
            await CatalogIndexBackfill.ReindexAsync<Order, OrderIndex>(
                session,
                OrderConstants.CollectionName,
                pageSize: 2,
                filter: index => index.Status == OrderStatus.Draft || index.Status == OrderStatus.AwaitingPayment);
        }

        // Assert
        Assert.True(_hook.Fired);

        await using var check = _store.CreateSession();
        var indexed = (await check.QueryIndex<OrderIndex>(index => index.BudgetCommittedUtc != null, OrderConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken))
            .Select(index => index.ItemId)
            .Order(StringComparer.Ordinal);

        Assert.Equal(itemIds, indexed);
    }

    private async Task ClearBudgetColumnAsync()
    {
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var dialect = _store.Configuration.SqlDialect;
        await using var command = transaction.Connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = $"UPDATE {dialect.QuoteForTableName(OrderConstants.CollectionName + "_" + nameof(OrderIndex), _store.Configuration.Schema)} SET {dialect.QuoteForColumnName(nameof(OrderIndex.BudgetCommittedUtc))} = NULL";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddAsync(string itemId, OrderStatus status)
    {
        await using var session = _store.CreateSession();
        await new OrderStore(session).CreateAsync(new Order
        {
            ItemId = itemId,
            Status = status,
            Currency = "USD",
            Subtotal = 30m,
            CreatedUtc = _heldUtc.AddDays(-1),
            BudgetHold = new OrderBudgetHold { Amount = 30m, Currency = "USD", HeldUtc = _heldUtc },
        }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // What a payment webhook does: reads the order in a session of its own, saves it as Paid with the store's version
    // check, and commits.
    private async Task PayAsync(string itemId)
    {
        await using var session = _store.CreateSession();
        var orders = new OrderStore(session);
        var order = await orders.FindByIdAsync(itemId, TestContext.Current.CancellationToken);

        order.Status = OrderStatus.Paid;
        order.PaidUtc = _heldUtc.AddDays(1);
        await orders.UpdateAsync(order, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // An index whose map, once armed, runs a save of another session the first time an unpaid order is mapped: YesSql maps
    // a session's records when it saves its changes, after the records were read and before the session's transaction
    // begins, so that save lands between the backfill's read and its commit.
    private sealed class ConcurrentSaveIndexProvider : IndexProvider<Order>
    {
        private string _itemId;
        private Func<Task> _save;

        public ConcurrentSaveIndexProvider()
        {
            CollectionName = OrderConstants.CollectionName;
        }

        public bool Fired { get; private set; }

        public void Arm(string itemId, Func<Task> save)
        {
            _itemId = itemId;
            _save = save;
        }

        public override void Describe(DescribeContext<Order> context)
            => context
                .For<ConcurrentSaveIndex>()
                .Map(order =>
                {
                    if (!Fired && _save is not null && order.ItemId == _itemId && order.Status != OrderStatus.Paid)
                    {
                        Fired = true;
                        Task.Run(_save).GetAwaiter().GetResult();
                    }

                    return new ConcurrentSaveIndex { ItemId = order.ItemId };
                });
    }

    private sealed class ConcurrentSaveIndex : MapIndex
    {
        public string ItemId { get; set; }
    }
}
