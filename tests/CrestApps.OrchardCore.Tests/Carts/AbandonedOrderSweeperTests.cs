using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Tests.Customers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// An unpaid order whose checkout ended without it hearing of it is canceled, so it stops holding anything; one whose
/// checkout is still open is left to it.
/// </summary>
public sealed class AbandonedOrderSweeperTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderStore _orders = new();
    private readonly Dictionary<string, CheckoutSession> _sessions = [];

    private AbandonedOrderSweeper CreateSweeper()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions
            .Setup(store => store.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => _sessions.GetValueOrDefault(id));

        var service = new OrderService(_orders, [], clock.Object, NullLogger<OrderService>.Instance);

        return new AbandonedOrderSweeper(_orders, service, sessions.Object, clock.Object, NullLogger<AbandonedOrderSweeper>.Instance);
    }

    private Order AddOrder(string id, OrderStatus status, CheckoutSessionStatus? session, TimeSpan age)
    {
        var order = new Order { ItemId = id, OrderNumber = id, Status = status, CreatedUtc = _now - age };

        if (session is { } sessionStatus)
        {
            order.CheckoutSessionId = "session-" + id;
            _sessions[order.CheckoutSessionId] = new CheckoutSession { SessionId = order.CheckoutSessionId, Status = sessionStatus };
        }

        _orders.Orders.Add(order);

        return order;
    }

    [Fact]
    public async Task OrdersWhoseCheckoutEnded_AreCanceled_WithTheReason()
    {
        var failed = AddOrder("failed", OrderStatus.AwaitingPayment, CheckoutSessionStatus.Failed, TimeSpan.FromHours(2));
        var expired = AddOrder("expired", OrderStatus.AwaitingPayment, CheckoutSessionStatus.Expired, TimeSpan.FromHours(2));
        var gone = AddOrder("gone", OrderStatus.AwaitingPayment, CheckoutSessionStatus.Pending, TimeSpan.FromHours(2));
        _sessions.Remove(gone.CheckoutSessionId);
        var neverStarted = AddOrder("draft", OrderStatus.Draft, session: null, TimeSpan.FromHours(2));

        var canceled = await CreateSweeper().SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, canceled);
        Assert.Equal(OrderCancelReason.PaymentFailed, failed.CancelReason);
        Assert.Equal(OrderCancelReason.CheckoutExpired, expired.CancelReason);
        Assert.Equal(OrderCancelReason.Abandoned, gone.CancelReason);
        Assert.Equal(OrderCancelReason.Abandoned, neverStarted.CancelReason);
    }

    [Fact]
    public async Task OpenCheckouts_RecentDrafts_AndPaidOrders_AreLeftAlone()
    {
        var open = AddOrder("open", OrderStatus.AwaitingPayment, CheckoutSessionStatus.AwaitingProvider, TimeSpan.FromHours(2));
        var paying = AddOrder("paying", OrderStatus.AwaitingPayment, CheckoutSessionStatus.PaymentPending, TimeSpan.FromHours(2));
        var recent = AddOrder("recent", OrderStatus.Draft, session: null, TimeSpan.FromMinutes(5));
        var paid = AddOrder("paid", OrderStatus.Paid, CheckoutSessionStatus.Completed, TimeSpan.FromHours(2));

        Assert.Equal(0, await CreateSweeper().SweepAsync(TestContext.Current.CancellationToken));

        Assert.All([open, paying, recent], order => Assert.Null(order.CancelReason));
        Assert.Equal(OrderStatus.Paid, paid.Status);
    }

    [Fact]
    public async Task OpenOrders_DoNotHideTheAbandonedOnesBehindThem()
    {
        // More open orders than a page, all newer than the abandoned one, which the store lists last.
        for (var i = 0; i < AbandonedOrderSweeper.PageSize + 5; i++)
        {
            AddOrder("open-" + i, OrderStatus.AwaitingPayment, CheckoutSessionStatus.Pending, TimeSpan.FromHours(2) - TimeSpan.FromSeconds(i));
        }

        var abandoned = AddOrder("abandoned", OrderStatus.AwaitingPayment, CheckoutSessionStatus.Canceled, TimeSpan.FromHours(3));

        Assert.Equal(1, await CreateSweeper().SweepAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OrderCancelReason.CheckoutCanceled, abandoned.CancelReason);
    }
}
