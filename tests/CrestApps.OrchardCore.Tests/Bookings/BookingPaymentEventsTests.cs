using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Bookings.Handlers;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.Workflows;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// Bookings report each payment, declined balance charge and refund to the payment handlers once, and the workflow events
/// get the booking and the money in their input.
/// </summary>
public sealed class BookingPaymentEventsTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<Booking> _bookings = [];
    private readonly RecordingPaymentHandler _recorder = new();
    private BookingBalance _balance;

    [Fact]
    public async Task ConfirmPaidAsync_ReportsTheFullPaymentOnce_WhenTheOrderIsReportedPaidTwice()
    {
        // Arrange
        _bookings.Add(NewBooking("b1", BookingStatus.Held, total: 300m));
        _bookings.Add(NewBooking("b2", BookingStatus.Held, total: 200m));

        var service = CreateService();
        var order = NewOrder();

        // Act
        await service.ConfirmPaidAsync(order, TestContext.Current.CancellationToken);
        await service.ConfirmPaidAsync(order, TestContext.Current.CancellationToken);

        // Assert
        var received = Assert.Single(_recorder.Received);

        Assert.Equal(BookingPaymentKind.FullPayment, received.PaymentKind);
        Assert.Equal(500m, received.Amount);
        Assert.Equal("USD", received.Currency);
        Assert.Equal("session1", received.Reference);
        Assert.Equal(["b1", "b2"], received.Bookings.Select(booking => booking.ItemId));
        Assert.All(received.Bookings, booking => Assert.Equal(BookingStatus.Confirmed, booking.Status));
    }

    [Fact]
    public async Task ConfirmPaidAsync_ReportsADeposit_WhenTheRestIsOwedLater()
    {
        // Arrange
        _bookings.Add(NewBooking("b1", BookingStatus.Held, total: 500m));

        var service = CreateService();
        var order = NewOrder();

        order.Tenders = [new OrderTender { ProviderKey = BookingsConstants.BalanceTenderKey, Amount = 400m }];

        // Act
        await service.ConfirmPaidAsync(order, TestContext.Current.CancellationToken);

        // Assert
        var received = Assert.Single(_recorder.Received);

        Assert.Equal(BookingPaymentKind.Deposit, received.PaymentKind);
        Assert.Equal(100m, received.Amount);
    }

    [Fact]
    public async Task ReportRefundAsync_ReportsTheRefund_ForTheBookingsThatWereConfirmed()
    {
        // Arrange
        _bookings.Add(NewBooking("paid", BookingStatus.Canceled, total: 300m, confirmed: true));
        _bookings.Add(NewBooking("held", BookingStatus.Held, total: 200m));

        var service = CreateService();

        // Act
        await service.ReportRefundAsync(NewOrder(), new OrderRefund { RefundId = "re_1", Amount = 120m, Currency = "USD", Reason = "Canceled" }, TestContext.Current.CancellationToken);
        await service.ReportRefundAsync(NewOrder(), new OrderRefund { RefundId = "re_2", Amount = 0m, Currency = "USD" }, TestContext.Current.CancellationToken);

        // Assert
        var refunded = Assert.Single(_recorder.Refunded);

        Assert.Null(refunded.PaymentKind);
        Assert.Equal(120m, refunded.Amount);
        Assert.Equal("Canceled", refunded.Reason);
        Assert.Equal("re_1", refunded.Reference);
        Assert.Equal("paid", Assert.Single(refunded.Bookings).ItemId);
    }

    [Fact]
    public async Task ReportRefundAsync_ReportsNothing_WhenNoBookingWasConfirmed()
    {
        // Arrange
        _bookings.Add(NewBooking("held", BookingStatus.Held, total: 200m));

        var service = CreateService();

        // Act
        await service.ReportRefundAsync(NewOrder(), new OrderRefund { RefundId = "re_1", Amount = 50m, Currency = "USD" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_recorder.Refunded);
    }

    [Fact]
    public async Task ChargeAsync_ReportsAFinalFailureOnce_WhenTheChargeIsRunAgain()
    {
        // Arrange
        _bookings.Add(NewBooking("b1", BookingStatus.Confirmed, total: 500m, confirmed: true));
        _balance = NewBalance();

        var charger = CreateCharger();

        // Act: the balance's invoice is gone, so the charge fails for good, and running it again changes nothing.
        var first = await charger.ChargeAsync(_balance, TestContext.Current.CancellationToken);
        var second = await charger.ChargeAsync(_balance, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(BookingBalanceStatus.Failed, _balance.Status);

        var failed = Assert.Single(_recorder.Failed);

        Assert.Equal(BookingPaymentKind.Balance, failed.PaymentKind);
        Assert.True(failed.IsFinal);
        Assert.Equal(400m, failed.Amount);
        Assert.Equal("tx1", failed.Reference);
        Assert.Equal(first, failed.Reason);
        Assert.Equal("b1", Assert.Single(failed.Bookings).ItemId);
    }

    [Fact]
    public async Task BalancePaymentHandler_ReportsTheBalancePayment()
    {
        // Arrange
        _bookings.Add(NewBooking("b1", BookingStatus.Confirmed, total: 500m, confirmed: true));
        _balance = NewBalance();

        var handler = CreateBalancePaymentHandler();
        var transaction = NewBalanceTransaction();

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, new TransactionEvent { Amount = 400m }), TestContext.Current.CancellationToken);

        // Assert
        var received = Assert.Single(_recorder.Received);

        Assert.Equal(BookingPaymentKind.Balance, received.PaymentKind);
        Assert.Equal(400m, received.Amount);
        Assert.Equal("tx1", received.Reference);
        Assert.Equal("b1", Assert.Single(received.Bookings).ItemId);
    }

    [Fact]
    public async Task BalancePaymentHandler_IgnoresOtherTransactions()
    {
        // Arrange
        _bookings.Add(NewBooking("b1", BookingStatus.Confirmed, total: 500m, confirmed: true));
        _balance = NewBalance();

        var handler = CreateBalancePaymentHandler();
        var other = NewBalanceTransaction();

        other.ObligationId = "invoice";

        var unknown = NewBalanceTransaction();

        unknown.ItemId = "tx2";

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(other, new TransactionEvent { Amount = 400m }), TestContext.Current.CancellationToken);
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(unknown, new TransactionEvent { Amount = 400m }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_recorder.Received);
    }

    [Fact]
    public void WorkflowInput_CarriesTheBookingAndThePayment()
    {
        // Arrange
        var booking = NewBooking("b1", BookingStatus.Confirmed, total: 500m, confirmed: true);
        var context = new BookingPaymentContext
        {
            Bookings = [booking],
            PaymentKind = BookingPaymentKind.Balance,
            Amount = 400m,
            Currency = "USD",
            Reason = "The card was declined.",
            IsFinal = true,
            Reference = "tx1",
        };

        // Act
        var input = BookingPaymentWorkflowHandler.CreateInput(booking, context);

        // Assert
        Assert.Same(booking, input["Booking"]);
        Assert.Equal("b1", input["BookingId"]);
        Assert.Equal("1001", input["OrderNumber"]);
        Assert.Equal("Confirmed", input["Status"]);
        Assert.Equal(400m, input["Amount"]);
        Assert.Equal("USD", input["Currency"]);
        Assert.Equal("Balance", input["PaymentKind"]);
        Assert.Equal("The card was declined.", input["Reason"]);
        Assert.Equal(true, input["IsFinal"]);
        Assert.Equal("tx1", input["PaymentReference"]);
    }

    [Fact]
    public async Task WorkflowHandler_DoesNothing_OutsideAShellScope()
    {
        // Arrange
        var handler = new BookingPaymentWorkflowHandler();
        var context = new BookingPaymentContext { Bookings = [NewBooking("b1", BookingStatus.Confirmed, total: 1m, confirmed: true)] };

        // Act
        var exception = await Record.ExceptionAsync(() => handler.PaymentReceivedAsync(context, TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    private static Booking NewBooking(string id, BookingStatus status, decimal total, bool confirmed = false)
        => new()
        {
            ItemId = id,
            OrderId = "order1",
            OrderNumber = "1001",
            ContentItemId = "tour",
            Title = "City tour",
            SlotKeys = ["2026-12-09"],
            Seats = 1,
            Status = status,
            Total = total,
            Currency = "USD",
            StartsUtc = new DateTime(2026, 12, 9, 14, 0, 0, DateTimeKind.Utc),
            ConfirmedUtc = confirmed ? _now.AddDays(-1) : null,
        };

    private static Order NewOrder()
        => new()
        {
            ItemId = "order1",
            OrderNumber = "1001",
            Currency = "USD",
            Subtotal = 500m,
            Total = 500m,
            CheckoutSessionId = "session1",
            CustomerName = "Ada Lovelace",
            CustomerEmail = "ada@example.com",
        };

    private static BookingBalance NewBalance()
        => new()
        {
            ItemId = "balance1",
            OrderId = "order1",
            OrderNumber = "1001",
            TransactionId = "tx1",
            Amount = 400m,
            Currency = "USD",
            DueUtc = _now,
            Status = BookingBalanceStatus.Scheduled,
        };

    private static Transaction NewBalanceTransaction()
        => new()
        {
            ItemId = "tx1",
            Source = BookingsConstants.TransactionSource,
            ObligationId = BookingBalanceTenderProvider.ObligationId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order1",
            Currency = "USD",
        };

    private Mock<IBookingStore> CreateBookingStore()
    {
        var bookings = new Mock<IBookingStore>();

        bookings.Setup(store => store.ListByOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string orderId, CancellationToken _) => [.. _bookings.Where(booking => booking.OrderId == orderId)]);
        bookings.Setup(store => store.UpdateAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return bookings;
    }

    private Mock<IBookingBalanceStore> CreateBalanceStore()
    {
        var balances = new Mock<IBookingBalanceStore>();

        balances.Setup(store => store.FindByOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string orderId, CancellationToken _) => _balance?.OrderId == orderId ? _balance : null);
        balances.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_balance?.ItemId == id ? _balance : null));
        balances.Setup(store => store.UpdateAsync(It.IsAny<BookingBalance>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return balances;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();

        clock.Setup(c => c.UtcNow).Returns(_now);

        return clock.Object;
    }

    private BookingService CreateService()
    {
        var bookings = CreateBookingStore();
        var clock = CreateClock();
        var locks = new Mock<IDistributedLock>();

        locks.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var slots = new Mock<IBookingSlotStore>();

        slots.Setup(store => store.ListAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var capacity = new BookingCapacityService(slots.Object, bookings.Object, Mock.Of<ISession>(), locks.Object, siteService.Object, clock, NullLogger<BookingCapacityService>.Instance);
        var emails = new BookingEmailSender(Mock.Of<ICommerceEmailSender>(), siteService.Object, HtmlEncoder.Default, new PassThroughLocalizer<BookingEmailSender>());

        return new BookingService(
            bookings.Object,
            CreateBalanceStore().Object,
            capacity,
            Mock.Of<IContentManager>(),
            Mock.Of<IOrderStore>(),
            Mock.Of<IOrderService>(),
            emails,
            clock,
            NullLogger<BookingService>.Instance,
            new PassThroughLocalizer<BookingService>(),
            Mock.Of<IOrderRefunder>(),
            Mock.Of<ITransactionManager>(),
            Mock.Of<ITransactionCommandService>(),
            [_recorder]);
    }

    private BookingBalanceCharger CreateCharger()
    {
        var transactions = new Mock<ITransactionManager>();

        transactions.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<Transaction>(null));

        return new BookingBalanceCharger(
            CreateBalanceStore().Object,
            CreateBookingStore().Object,
            transactions.Object,
            Mock.Of<ICheckoutEngine>(),
            Mock.Of<ICheckoutSessionStore>(),
            Mock.Of<IPaymentAttemptStore>(),
            [],
            CreateClock(),
            NullLogger<BookingBalanceCharger>.Instance,
            new PassThroughLocalizer<BookingBalanceCharger>(),
            [_recorder]);
    }

    private BookingBalancePaymentHandler CreateBalancePaymentHandler()
        => new(CreateBalanceStore().Object, CreateBookingStore().Object, [_recorder], NullLogger<BookingBalancePaymentHandler>.Instance);

    private sealed class RecordingPaymentHandler : IBookingPaymentHandler
    {
        public List<BookingPaymentContext> Received { get; } = [];

        public List<BookingPaymentContext> Failed { get; } = [];

        public List<BookingPaymentContext> Refunded { get; } = [];

        public Task PaymentReceivedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        {
            Received.Add(context);

            return Task.CompletedTask;
        }

        public Task PaymentFailedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        {
            Failed.Add(context);

            return Task.CompletedTask;
        }

        public Task RefundedAsync(BookingPaymentContext context, CancellationToken cancellationToken = default)
        {
            Refunded.Add(context);

            return Task.CompletedTask;
        }
    }

    private sealed class NoopLocker : ILocker
    {
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
