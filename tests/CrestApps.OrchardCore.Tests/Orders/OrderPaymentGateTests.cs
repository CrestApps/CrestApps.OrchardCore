using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// An order's pay link opened on two devices starts two checkouts of the same order. Only one of them may take the
/// money: the other is refused while the first payment may still go through, and once the order is paid.
/// </summary>
public sealed class OrderPaymentGateTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Order _order = new() { ItemId = "order-1", Status = OrderStatus.AwaitingPayment };
    private readonly InMemoryCheckoutSessionStore _sessions = new();
    private readonly InMemoryPaymentAttemptStore _attempts = new();
    private readonly TestClock _clock = new(_now);

    public OrderPaymentGateTests()
    {
        _sessions.Seed(Session("session-a"));
        _sessions.Seed(Session("session-b"));
    }

    [Fact]
    public async Task ASecondCheckoutOfTheOrder_IsRefused_WhileTheFirstPaymentMayStillGoThrough()
    {
        // Arrange
        var gate = CreateGate();
        var first = await ContextAsync("session-a");

        await gate.BeginningAsync(first, TestContext.Current.CancellationToken);
        await _attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "attempt-a",
            SessionId = "session-a",
            ObligationId = CheckoutObligations.OneTime,
            ProviderKey = "card",
            State = PaymentAttemptState.Pending,
            CreatedUtc = _now,
        }, TestContext.Current.CancellationToken);

        var second = await ContextAsync("session-b");

        // Act
        await gate.BeginningAsync(second, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(first.IsRefused);
        Assert.True(second.IsRefused);
    }

    [Fact]
    public async Task ACheckoutOfAnOrderAlreadyPaid_IsRefused()
    {
        // Arrange
        _order.Status = OrderStatus.Paid;
        var context = await ContextAsync("session-b");

        // Act
        await CreateGate().BeginningAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.IsRefused);
    }

    [Fact]
    public async Task TheSameCheckout_MayBeginAgain()
    {
        // Arrange
        var gate = CreateGate();
        await gate.BeginningAsync(await ContextAsync("session-a"), TestContext.Current.CancellationToken);

        var again = await ContextAsync("session-a");

        // Act
        await gate.BeginningAsync(again, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(again.IsRefused);
    }

    private OrderPaymentGate CreateGate()
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);

        var distributedLock = new LocalLock(NullLogger<LocalLock>.Instance);
        var holds = new CheckoutPaymentHoldService(_attempts, _sessions, Mock.Of<ICheckoutPaymentReleaseService>(), distributedLock, _clock);

        return new OrderPaymentGate(orders.Object, holds, distributedLock, Mock.Of<ISession>(), new PassThroughStringLocalizer<OrderPaymentGate>());
    }

    private async Task<CheckoutPaymentBeginningContext> ContextAsync(string sessionId)
        => new(new CheckoutFlow(await _sessions.GetAsync(sessionId, TestContext.Current.CancellationToken)), "card");

    private static CheckoutSession Session(string sessionId)
        => new()
        {
            SessionId = sessionId,
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order-1",
            Status = CheckoutSessionStatus.Pending,
            Currency = "USD",
        };
}
