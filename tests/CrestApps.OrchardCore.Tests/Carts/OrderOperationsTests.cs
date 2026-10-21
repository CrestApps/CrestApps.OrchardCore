using CrestApps.Core.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// What the store does to an order from its page: refunds that show on the order with who gave them, and cancels
/// that give the money back, cancel what Pay Later still has to collect, and can put the stock back.
/// </summary>
public sealed class OrderOperationsTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderStore _orders = new();
    private readonly RecordingHandler _handler = new();

    [Fact]
    public async Task ARefund_ShowsOnTheOrder_Once_AndOnlyAFullRefundPutsTheStockBack()
    {
        var order = PaidOrder();
        var service = OrderService();

        Assert.True(await service.RecordRefundAsync(order, Refund("r1", 50m), restock: true, TestContext.Current.CancellationToken));
        Assert.Equal(OrderRefundStatus.PartiallyRefunded, order.RefundStatus);
        Assert.False(_handler.Refunds[^1].Restock);

        // Reported again when it is reconciled: counted once.
        Assert.False(await service.RecordRefundAsync(order, Refund("r1", 50m), restock: true, TestContext.Current.CancellationToken));

        Assert.True(await service.RecordRefundAsync(order, Refund("r2", 186.50m), restock: true, TestContext.Current.CancellationToken));
        Assert.Equal(OrderRefundStatus.Refunded, order.RefundStatus);
        Assert.Equal(236.50m, order.RefundedAmount);
        Assert.True(_handler.Refunds[^1].Restock);
        Assert.Equal(2, _handler.Refunds.Count);
    }

    [Fact]
    public async Task OnlyTheStore_CancelsAPaidOrder()
    {
        var order = PaidOrder();
        var service = OrderService();

        Assert.False(await service.CancelAsync(order, OrderCancelReason.CheckoutCanceled, TestContext.Current.CancellationToken));
        Assert.Equal(OrderStatus.Paid, order.Status);

        Assert.True(await service.CancelAsync(order, OrderCancelReason.CanceledByStore, restock: true, TestContext.Current.CancellationToken));
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.True(Assert.Single(_handler.Cancels).Restock);
    }

    [Fact]
    public async Task RefundingFromTheOrder_RefundsItsPayment_UpToWhatIsLeft()
    {
        var order = PaidOrder();
        var refunds = new List<RequestPaymentRefundContext>();
        var services = CardPaymentServices(refunds);
        var operations = Operations(services);

        var tooMuch = await operations.RefundAsync(order, 300m, "Too much", restock: false, "admin", TestContext.Current.CancellationToken);
        Assert.False(tooMuch.Succeeded);
        Assert.Empty(refunds);

        var result = await operations.RefundAsync(order, 50m, "Scratched strap", restock: false, "admin", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(50m, result.Refunded);
        var refund = Assert.Single(refunds);
        Assert.Equal(50m, refund.Amount);
        Assert.Equal("pi_1", refund.OriginalTransactionId);
        Assert.Equal("Scratched strap", refund.Reason);
    }

    [Fact]
    public async Task CancelingAPayLaterOrder_CancelsWhatIsStillOwed_AndSaysWhoDidIt()
    {
        var order = PaidOrder();
        var commands = new Mock<ITransactionCommandService>();
        commands.Setup(value => value.CancelAsync("tx-1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TransactionCommandResult());

        var transactions = new Mock<ITransactionStore>();
        transactions
            .Setup(value => value.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TransactionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<Transaction>
            {
                Count = 1,
                Entries = [new Transaction { ItemId = "tx-1", Status = TransactionStatus.Outstanding, TotalAmount = 236.50m, Amount = 236.50m, Currency = "USD" }],
            });

        var services = new ServiceCollection()
            .AddSingleton(commands.Object)
            .AddSingleton(transactions.Object)
            .BuildServiceProvider();

        var money = await Operations(services).GetMoneyAsync(order, TestContext.Current.CancellationToken);
        Assert.Equal(236.50m, money.Outstanding);
        Assert.Equal(0m, money.Refundable);

        var result = await Operations(services).CancelAsync(order, "Out of stock", refund: true, restock: true, "admin", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(OrderCancelReason.CanceledByStore, order.CancelReason);
        Assert.Equal("Out of stock", order.CancelNote);
        commands.Verify(value => value.CancelAsync("tx-1", "Out of stock", It.IsAny<CancellationToken>()), Times.Once);

        var note = Assert.Single(order.Notes);
        Assert.Equal("admin", note.AuthorName);
        Assert.Contains("Out of stock", note.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefundStartedAnywhere_IsRecordedOnItsOrder_WithWhoGaveIt()
    {
        var order = PaidOrder();
        var scope = new OrderOperationScope { ActorName = "admin" };

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions
            .Setup(value => value.GetManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = order.ItemId }]);

        var recorder = new OrderRefundRecorder(
            _orders,
            OrderService(),
            scope,
            new ServiceCollection().BuildServiceProvider(),
            Clock(),
            new PassThroughLocalizer<OrderRefundRecorder>(),
            sessions.Object);

        var refund = new PaymentRefund { ItemId = "refund-1", SessionId = "session-1", RefundGrossAmount = 50m, Currency = "USD", Reason = "Scratched strap" };

        await recorder.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);
        await recorder.RefundSucceededAsync(refund, TestContext.Current.CancellationToken);

        Assert.Equal(50m, Assert.Single(order.Refunds).Amount);
        Assert.Equal(OrderRefundStatus.PartiallyRefunded, order.RefundStatus);

        var note = Assert.Single(order.Notes);
        Assert.Equal("admin", note.AuthorName);
        Assert.Equal(OrderNoteKind.Event, note.Kind);
        Assert.Contains("Scratched strap", note.Text, StringComparison.Ordinal);
    }

    private OrderService OrderService()
        => new(_orders, [_handler], Clock(), NullLogger<OrderService>.Instance);

    private OrderOperationsService Operations(IServiceProvider services)
        => new(
            _orders,
            OrderService(),
            new OrderOperationScope(),
            confirmationSender: null,
            services.GetService<ICheckoutEngine>() ?? Mock.Of<ICheckoutEngine>(),
            services.GetService<ICheckoutTenderLedger>() ?? NoTenders(),
            services.GetService<ICheckoutRefundService>() ?? Mock.Of<ICheckoutRefundService>(),
            services.GetService<IPaymentAttemptStore>() ?? Mock.Of<IPaymentAttemptStore>(),
            services.GetService<IPaymentRefundStore>() ?? Mock.Of<IPaymentRefundStore>(),
            services.GetService<ICheckoutPaymentProviderResolver>() ?? Mock.Of<ICheckoutPaymentProviderResolver>(),
            Clock(),
            services,
            NullLogger<OrderOperationsService>.Instance,
            new PassThroughLocalizer<OrderOperationsService>());

    private static ICheckoutTenderLedger NoTenders()
    {
        var ledger = new Mock<ICheckoutTenderLedger>();
        ledger
            .Setup(value => value.GetRedeemedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return ledger.Object;
    }

    private static ServiceProvider CardPaymentServices(List<RequestPaymentRefundContext> refunds)
    {
        var attempts = new Mock<IPaymentAttemptStore>();
        attempts
            .Setup(value => value.GetBySessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PaymentAttempt { ItemId = "a1", SessionId = "session-1", State = PaymentAttemptState.Succeeded, TransactionId = "pi_1", ProviderKey = "test", ConfirmedAmount = 215m, ConfirmedTaxAmount = 21.50m, Currency = "USD", CreatedUtc = _now }]);

        var refundStore = new Mock<IPaymentRefundStore>();
        refundStore.Setup(value => value.GetByOriginalTransactionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var refundService = new Mock<ICheckoutRefundService>();
        refundService
            .Setup(value => value.RequestRefundAsync(It.IsAny<RequestPaymentRefundContext>(), It.IsAny<CancellationToken>()))
            .Callback<RequestPaymentRefundContext, CancellationToken>((context, _) => refunds.Add(context))
            .ReturnsAsync((RequestPaymentRefundContext context, CancellationToken _) => new PaymentRefund { ItemId = "refund-" + refunds.Count, Status = RefundStatus.Succeeded, RefundGrossAmount = context.Amount ?? 0m, Currency = "USD" });

        return new ServiceCollection()
            .AddSingleton(attempts.Object)
            .AddSingleton(refundStore.Object)
            .AddSingleton(refundService.Object)
            .BuildServiceProvider();
    }

    private Order PaidOrder()
    {
        var order = new Order
        {
            ItemId = "order-1",
            OrderNumber = "261018-ABC123",
            Status = OrderStatus.Paid,
            Currency = "USD",
            Total = 236.50m,
            CheckoutSessionId = "session-1",
            CustomerEmail = "pat@example.test",
        };

        _orders.Orders.Add(order);

        return order;
    }

    private static OrderRefund Refund(string id, decimal amount)
        => new() { RefundId = id, Amount = amount, Currency = "USD", CreatedUtc = _now };

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed class RecordingHandler : OrderEventHandlerBase
    {
        public List<OrderRefundedContext> Refunds { get; } = [];

        public List<OrderCanceledContext> Cancels { get; } = [];

        public override Task RefundedAsync(OrderRefundedContext context, CancellationToken cancellationToken = default)
        {
            Refunds.Add(context);

            return Task.CompletedTask;
        }

        public override Task CanceledAsync(OrderCanceledContext context, CancellationToken cancellationToken = default)
        {
            Cancels.Add(context);

            return Task.CompletedTask;
        }
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
