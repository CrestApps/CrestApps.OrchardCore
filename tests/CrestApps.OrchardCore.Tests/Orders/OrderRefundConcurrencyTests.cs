using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// SEC-C17e (review): a double submit of "refund as store credit". The second request loaded the order before the first
/// committed; after taking the lock it read the order again, but through its own session, whose identity map handed back
/// the copy from before the first refund. So the store credit was given twice. The order is now detached before the
/// read, so the read comes from the database.
/// </summary>
public sealed class OrderRefundConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"order-refund-{Guid.NewGuid():N}.db");
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private IStore _store;
    private int _credited;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new OrderIndexProvider()], OrderConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OrderConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            await new OrderMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await new OrderStore(session).CreateAsync(new Order
            {
                ItemId = "order-1",
                OrderNumber = "261112-ABC123",
                Status = OrderStatus.Paid,
                Currency = "USD",
                Total = 236.50m,
                CheckoutSessionId = "session-1",
                CustomerEmail = "pat@example.test",
                CreatedUtc = _now,
            }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ASecondRefundAsStoreCredit_ThatReadTheOrderBeforeTheFirstCommitted_GivesNoSecondCredit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange: request B loaded the order while request A was refunding it.
        await using var second = _store.CreateSession();
        var secondOrder = await new OrderStore(second).FindByIdAsync("order-1", cancellationToken);

        await using (var first = _store.CreateSession())
        {
            var firstOrder = await new OrderStore(first).FindByIdAsync("order-1", cancellationToken);
            var refunded = await Operations(first).RefundAsync(firstOrder, amount: null, "Damaged", restock: false, "admin", asStoreCredit: true, cancellationToken);

            Assert.True(refunded.Succeeded);
        }

        // Act: B takes the lock A let go and refunds the whole order again.
        var exception = await Record.ExceptionAsync(() => Operations(second).RefundAsync(secondOrder, amount: null, "Damaged", restock: false, "admin", asStoreCredit: true, cancellationToken));

        // Assert
        Assert.Equal(1, _credited);
        Assert.Null(exception);
    }

    private OrderOperationsService Operations(ISession session)
    {
        var orders = new OrderStore(session);
        var clock = Mock.Of<IClock>(value => value.UtcNow == _now);

        var refunder = new Mock<IOrderStoreCreditRefunder>();
        refunder.Setup(value => value.CanRefundAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        refunder.Setup(value => value.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref _credited);

                return true;
            });

        var attempts = new Mock<IPaymentAttemptStore>();
        attempts.Setup(value => value.GetBySessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PaymentAttempt { ItemId = "a1", SessionId = "session-1", State = PaymentAttemptState.Succeeded, TransactionId = "pi_1", ProviderKey = "test", ConfirmedAmount = 215m, ConfirmedTaxAmount = 21.50m, Currency = "USD", CreatedUtc = _now }]);

        var refundStore = new Mock<IPaymentRefundStore>();
        refundStore.Setup(value => value.GetByOriginalTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var tenders = new Mock<ICheckoutTenderLedger>();
        tenders.Setup(value => value.GetRedeemedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var services = new ServiceCollection()
            .AddSingleton(session)
            .AddSingleton(refunder.Object)
            .BuildServiceProvider();

        var localizer = new Mock<IStringLocalizer<OrderOperationsService>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        return new OrderOperationsService(
            orders,
            new OrderService(orders, [], new InMemoryNumberSequence(), clock, NullLogger<OrderService>.Instance),
            new OrderOperationScope(),
            confirmationSender: null,
            Mock.Of<ICheckoutEngine>(),
            tenders.Object,
            Mock.Of<ICheckoutRefundService>(),
            attempts.Object,
            refundStore.Object,
            Mock.Of<ICheckoutPaymentProviderResolver>(),
            _locks,
            clock,
            services,
            NullLogger<OrderOperationsService>.Instance,
            localizer.Object);
    }
}
