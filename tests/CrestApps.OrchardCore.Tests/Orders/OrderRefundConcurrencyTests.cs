using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
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
    private readonly GiftCardTenderProvider _giftCards = new();
    private IStore _store;
    private int _credited;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new OrderIndexProvider()], OrderConstants.CollectionName);
        _store.RegisterIndexes([new CheckoutTenderHoldIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(OrderConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            await new OrderMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await new CheckoutTenderMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
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

    /// <summary>
    /// M3 (final review): only the order was read again inside the refund lock. A request that loaded the gift card holds
    /// before the lock (a cancel with refund, a booking cancel and a till return all do) got that copy back from its
    /// session, which said nothing was returned, so the card was credited a second time. The real ledger, over SQLite.
    /// </summary>
    [Fact]
    public async Task ASecondRefund_ThatReadTheGiftCardHoldsBeforeTheFirstCommitted_CreditsTheCardOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange: the order was paid 50 with a gift card. Request B loaded the order and what can be refunded while
        // request A was refunding it.
        await SeedGiftCardHoldAsync(50m);

        await using var second = _store.CreateSession();
        var secondOrder = await new OrderStore(second).FindByIdAsync("order-1", cancellationToken);
        var secondOperations = Operations(second, tenders: Ledger(second), paidByCard: false);
        Assert.Equal(50m, (await secondOperations.GetMoneyAsync(secondOrder, cancellationToken)).Refundable);

        await using (var first = _store.CreateSession())
        {
            var firstOrder = await new OrderStore(first).FindByIdAsync("order-1", cancellationToken);
            var refunded = await Operations(first, tenders: Ledger(first), paidByCard: false).RefundAsync(firstOrder, amount: null, "Damaged", restock: false, "admin", asStoreCredit: false, cancellationToken);

            Assert.True(refunded.Succeeded, string.Join(" ", refunded.Errors));
        }

        // Act: B takes the lock A let go and refunds the whole order again.
        await secondOperations.RefundAsync(secondOrder, amount: null, "Damaged", restock: false, "admin", asStoreCredit: false, cancellationToken);

        // Assert
        Assert.Equal([50m], _giftCards.Credits);
    }

    /// <summary>
    /// L-checkout-refund-interrupted (final review): only the final commit ignored the request's token. A request aborted
    /// after the store credit was given failed in what came after, so the order showed no refund and a retry credited again.
    /// </summary>
    [Fact]
    public async Task ARefundAsStoreCredit_AbortedAfterTheCreditWasGiven_IsStillRecordedOnTheOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var aborted = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var orders = new OrderStore(session);
            var clock = Mock.Of<IClock>(value => value.UtcNow == _now);

            // A handler of the refund that reads the database with the request's token, as the booking's handler does.
            var orderService = new OrderService(orders, [new TokenObservingHandler()], new InMemoryNumberSequence(), clock, NullLogger<OrderService>.Instance);
            var order = await orders.FindByIdAsync("order-1", cancellationToken);

            // Act: the request is aborted just after the credit was given.
            var exception = await Record.ExceptionAsync(() => Operations(session, orderService: orderService, onCredit: aborted.Cancel)
                .RefundAsync(order, amount: null, "Damaged", restock: false, "admin", asStoreCredit: true, aborted.Token));

            Assert.Null(exception);
        }

        // Assert
        await using var check = _store.CreateSession();
        var saved = await new OrderStore(check).FindByIdAsync("order-1", cancellationToken);

        Assert.Equal(1, _credited);
        Assert.Equal(236.50m, Assert.Single(saved.Refunds).Amount);
    }

    [Fact]
    public async Task CancelingAPaidBookingWithARefund_KeepsTheRefundAndItsNoteOnTheOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange: one request cancels a paid booking; the refund it gives commits, then the cancellation writes the order.
        await using var session = _store.CreateSession();
        var booking = new Booking
        {
            ItemId = "booking-1",
            OrderId = "order-1",
            OrderNumber = "261112-ABC123",
            ContentItemId = "tour-1",
            Title = "River tour",
            Status = BookingStatus.Confirmed,
            StartsUtc = _now.AddDays(30),
            Seats = 2,
            Total = 236.50m,
            Currency = "USD",
            CustomerEmail = "pat@example.test",
        };

        // Act
        var (succeeded, errors) = await Bookings(session, booking).CancelAsync(booking, refund: null, "Change of plans", "admin", cancellationToken);
        var exception = await Record.ExceptionAsync(() => session.SaveChangesAsync(cancellationToken));

        // Assert: what the request committed last still has the refund, its note and the cancellation's note.
        Assert.True(succeeded, string.Join(" ", errors));
        Assert.Null(exception);

        await using var check = _store.CreateSession();
        var order = await new OrderStore(check).FindByIdAsync("order-1", cancellationToken);

        Assert.Equal(236.50m, Assert.Single(order.Refunds).Amount);
        Assert.Contains(order.Notes, note => note.Text.StartsWith("Refunded", StringComparison.Ordinal));
        Assert.Contains(order.Notes, note => note.Text.Contains("canceled", StringComparison.Ordinal));
    }

    // The booking service over the real order refund: the order's refund operation, and a payment refund that, like the
    // checkout's, reports its success to the handler that records it on the order.
    private BookingService Bookings(ISession session, Booking booking)
    {
        var orders = new OrderStore(session);
        var clock = Mock.Of<IClock>(value => value.UtcNow == _now);
        var orderService = new OrderService(orders, [], new InMemoryNumberSequence(), clock, NullLogger<OrderService>.Instance);
        var scope = new OrderOperationScope();

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions.Setup(value => value.GetManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" }]);

        var recorder = new OrderRefundRecorder(orders, orderService, scope, new ServiceCollection().BuildServiceProvider(), clock, new PassThroughStringLocalizer<OrderRefundRecorder>(), sessions.Object);
        var refunds = new Mock<ICheckoutRefundService>();
        refunds.Setup(value => value.RequestRefundAsync(It.IsAny<RequestPaymentRefundContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (RequestPaymentRefundContext context, CancellationToken token) =>
            {
                var refund = new PaymentRefund { ItemId = "refund-1", SessionId = context.SessionId, RefundGrossAmount = context.Amount ?? 0m, Currency = "USD", Reason = context.Reason, Status = RefundStatus.Succeeded };

                await recorder.RefundSucceededAsync(refund, token);

                return refund;
            });

        var bookings = new Mock<IBookingStore>();
        bookings.Setup(value => value.FindByIdAsync(booking.ItemId, It.IsAny<CancellationToken>())).Returns((string _, CancellationToken _) => ValueTask.FromResult(booking));

        var slots = new Mock<IBookingSlotStore>();
        slots.Setup(value => value.ListAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new BookingService(
            bookings.Object,
            Mock.Of<IBookingBalanceStore>(),
            new BookingCapacityService(slots.Object, bookings.Object, session, _locks, siteService.Object, clock, NullLogger<BookingCapacityService>.Instance),
            Mock.Of<IContentManager>(),
            orders,
            orderService,
            new BookingEmailSender(Mock.Of<ICommerceEmailSender>(), siteService.Object, HtmlEncoder.Default, new PassThroughStringLocalizer<BookingEmailSender>()),
            clock,
            NullLogger<BookingService>.Instance,
            new PassThroughStringLocalizer<BookingService>(),
            new OrderRefunder(Operations(session, refunds.Object, orderService, scope)),
            Mock.Of<ITransactionManager>(),
            Mock.Of<ITransactionCommandService>(),
            []);
    }

    private async Task SeedGiftCardHoldAsync(decimal amount)
    {
        await using var session = _store.CreateSession();

        await session.SaveAsync(new CheckoutTenderHold
        {
            ItemId = "hold-1",
            SessionId = "session-1",
            ProviderKey = GiftCardTenderProvider.ProviderKey,
            Reference = "card-1",
            Amount = amount,
            Currency = "USD",
            Description = "Gift card",
            Status = CheckoutTenderHoldStatus.Redeemed,
            CreatedUtc = _now,
            RedeemedUtc = _now,
        }, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private CheckoutTenderLedger Ledger(ISession session)
        => new(session, _locks, [_giftCards], Mock.Of<IClock>(value => value.UtcNow == _now), NullLogger<CheckoutTenderLedger>.Instance);

    private OrderOperationsService Operations(ISession session, ICheckoutRefundService refundService = null, IOrderService orderService = null, OrderOperationScope scope = null, ICheckoutTenderLedger tenders = null, bool paidByCard = true, Action onCredit = null)
    {
        var orders = new OrderStore(session);
        var clock = Mock.Of<IClock>(value => value.UtcNow == _now);

        var refunder = new Mock<IOrderStoreCreditRefunder>();
        refunder.Setup(value => value.CanRefundAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        refunder.Setup(value => value.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref _credited);
                onCredit?.Invoke();

                return true;
            });

        var attempts = new Mock<IPaymentAttemptStore>();
        attempts.Setup(value => value.GetBySessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(!paidByCard ? [] : [new PaymentAttempt { ItemId = "a1", SessionId = "session-1", State = PaymentAttemptState.Succeeded, TransactionId = "pi_1", ProviderKey = "test", ConfirmedAmount = 215m, ConfirmedTaxAmount = 21.50m, Currency = "USD", CreatedUtc = _now }]);

        var refundStore = new Mock<IPaymentRefundStore>();
        refundStore.Setup(value => value.GetByOriginalTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        if (tenders is null)
        {
            var noTenders = new Mock<ICheckoutTenderLedger>();
            noTenders.Setup(value => value.GetRedeemedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            tenders = noTenders.Object;
        }

        var services = new ServiceCollection()
            .AddSingleton(session)
            .AddSingleton(refunder.Object)
            .BuildServiceProvider();

        var localizer = new Mock<IStringLocalizer<OrderOperationsService>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        return new OrderOperationsService(
            orders,
            orderService ?? new OrderService(orders, [], new InMemoryNumberSequence(), clock, NullLogger<OrderService>.Instance),
            scope ?? new OrderOperationScope(),
            confirmationSender: null,
            Mock.Of<ICheckoutEngine>(),
            tenders,
            refundService ?? Mock.Of<ICheckoutRefundService>(),
            attempts.Object,
            refundStore.Object,
            Mock.Of<ICheckoutPaymentProviderResolver>(),
            _locks,
            clock,
            services,
            NullLogger<OrderOperationsService>.Instance,
            localizer.Object);
    }

    // A handler that reads the database with the token it is given, as a real one does.
    private sealed class TokenObservingHandler : OrderEventHandlerBase
    {
        public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    // Gift cards whose balance is kept in memory; what is credited back is listed.
    private sealed class GiftCardTenderProvider : ICheckoutTenderProvider
    {
        public const string ProviderKey = "gift-card";

        public List<decimal> Credits { get; } = [];

        public string Key
            => ProviderKey;

        public Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenderLine>>([]);

        public Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
            => Task.FromResult<decimal?>(0m);

        public Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
        {
            lock (Credits)
            {
                Credits.Add(amount);
            }

            return Task.CompletedTask;
        }
    }
}
