using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A dispute a gateway reports against a payment is recorded on the order that payment paid for, once per change,
/// with a note, and an order no payment matches is left alone.
/// </summary>
public sealed class OrderDisputeRecorderTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderStore _orders = new();
    private readonly Mock<IOrderService> _orderService = new();
    private readonly List<string> _notes = [];

    public OrderDisputeRecorderTests()
    {
        _orderService
            .Setup(service => service.AddNoteAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<OrderNoteKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Order, string, OrderNoteKind, string, CancellationToken>((_, text, _, _, _) => _notes.Add(text))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ADisputeIsRecordedOnItsOrder_Once()
    {
        var order = PaidOrder();
        var recorder = CreateRecorder();
        var created = new PaymentDisputeCreatedContext
        {
            GatewayId = "Stripe",
            OriginalTransactionId = "pi_123",
            DisputeReference = "du_1",
            Amount = 54m,
            Currency = "usd",
            Reason = "fraudulent",
            Status = "needs_response",
        };

        await recorder.PaymentDisputeCreatedAsync(created, TestContext.Current.CancellationToken);
        await recorder.PaymentDisputeCreatedAsync(created, TestContext.Current.CancellationToken);

        var dispute = Assert.Single(order.Disputes);
        Assert.Equal("du_1", dispute.Reference);
        Assert.Equal(54m, dispute.Amount);
        Assert.Equal("USD", dispute.Currency);
        Assert.False(dispute.IsClosed);
        Assert.True(order.HasOpenDispute);
        Assert.Single(_notes);
        Assert.Contains("they don't recognize the payment", _notes[0]);
    }

    [Fact]
    public async Task AWonDisputeClosesIt()
    {
        var order = PaidOrder();
        var recorder = CreateRecorder();

        await recorder.PaymentDisputeCreatedAsync(new PaymentDisputeCreatedContext { GatewayId = "Stripe", OriginalTransactionId = "pi_123", DisputeReference = "du_1", Amount = 54m, Currency = "usd", Status = "needs_response" }, TestContext.Current.CancellationToken);
        await recorder.PaymentDisputeUpdatedAsync(new PaymentDisputeUpdatedContext { GatewayId = "Stripe", OriginalTransactionId = "pi_123", DisputeReference = "du_1", Amount = 54m, Currency = "usd", Status = "won", IsClosed = true }, TestContext.Current.CancellationToken);

        var dispute = Assert.Single(order.Disputes);
        Assert.True(dispute.IsClosed);
        Assert.Equal("won", dispute.Status);
        Assert.False(order.HasOpenDispute);
        Assert.Equal(2, _notes.Count);
        Assert.Contains("won, the money stays with you", _notes[1]);
    }

    [Fact]
    public async Task ADisputeOfAnotherPaymentIsIgnored()
    {
        var order = PaidOrder();

        await CreateRecorder().PaymentDisputeCreatedAsync(new PaymentDisputeCreatedContext { GatewayId = "Stripe", OriginalTransactionId = "pi_other", DisputeReference = "du_2", Amount = 10m, Currency = "usd" }, TestContext.Current.CancellationToken);

        Assert.Empty(order.Disputes);
        Assert.Empty(_notes);
    }

    private Order PaidOrder()
    {
        var order = new Order { ItemId = "order-1", OrderNumber = "261018-ABC123", Currency = "USD", Status = OrderStatus.Paid, CheckoutSessionId = "session-1" };
        _orders.Orders.Add(order);

        return order;
    }

    private OrderDisputeRecorder CreateRecorder()
    {
        var attempts = new InMemoryPaymentAttemptStore(new PaymentAttempt
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = "Stripe",
            ProviderReference = "pi_123",
            State = PaymentAttemptState.Succeeded,
        });

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions
            .Setup(store => store.GetManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Contains("session-1")
                ? [new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" }]
                : []);

        return new OrderDisputeRecorder(_orders, _orderService.Object, new TestClock(_now), new PassThroughStringLocalizer<OrderDisputeRecorder>(), attempts, sessions.Object);
    }
}
