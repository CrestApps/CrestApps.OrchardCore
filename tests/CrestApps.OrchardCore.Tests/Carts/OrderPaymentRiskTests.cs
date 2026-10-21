using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Handlers;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A paid order keeps the fraud rating of its riskiest payment, so a risky order is flagged before it ships.
/// </summary>
public sealed class OrderPaymentRiskTests
{
    [Fact]
    public async Task TheRiskiestPaymentSpeaksForTheOrder()
    {
        var order = await CompleteAsync(
            new PaymentAttempt { ItemId = "a1", SessionId = "session-1", ProviderKey = "Stripe", State = PaymentAttemptState.Succeeded, RiskLevel = "normal", RiskScore = 12 },
            new PaymentAttempt { ItemId = "a2", SessionId = "session-1", ProviderKey = "Stripe", State = PaymentAttemptState.Succeeded, RiskLevel = "elevated", RiskScore = 70 },
            new PaymentAttempt { ItemId = "a3", SessionId = "session-1", ProviderKey = "Stripe", State = PaymentAttemptState.Failed, RiskLevel = "highest", RiskScore = 95 });

        Assert.Equal("elevated", order.PaymentRisk.Level);
        Assert.Equal(70, order.PaymentRisk.Score);
        Assert.Equal("Stripe", order.PaymentRisk.Gateway);
        Assert.True(order.PaymentRisk.IsElevated);
    }

    [Fact]
    public async Task AnUnratedPaymentLeavesNoRating()
    {
        var order = await CompleteAsync(new PaymentAttempt { ItemId = "a1", SessionId = "session-1", ProviderKey = "pay-later", State = PaymentAttemptState.Succeeded });

        Assert.Null(order.PaymentRisk);
    }

    [Theory]
    [InlineData("normal", false)]
    [InlineData("elevated", true)]
    [InlineData("highest", true)]
    public void OnlyElevatedAndHighestAskForACheck(string level, bool elevated)
        => Assert.Equal(elevated, new OrderPaymentRisk { Level = level }.IsElevated);

    private static async Task<Order> CompleteAsync(params PaymentAttempt[] attempts)
    {
        var orders = new InMemoryOrderStore();
        var order = new Order { ItemId = "order-1", Status = OrderStatus.Paid, CheckoutSessionId = "session-1" };
        orders.Orders.Add(order);

        var services = new ServiceCollection()
            .AddSingleton<IPaymentAttemptStore>(new InMemoryPaymentAttemptStore(attempts))
            .BuildServiceProvider();

        var session = new CheckoutSession { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" };

        await new OrderPaymentRiskCheckoutHandler(orders, services).CompletedAsync(new CheckoutFlowCompletedContext(new CheckoutFlow(session)));

        return order;
    }
}
