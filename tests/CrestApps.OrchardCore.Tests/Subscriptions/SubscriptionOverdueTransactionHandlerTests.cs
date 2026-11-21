using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C5: nothing reacted to a transaction becoming overdue, so a subscription bought with Pay Later stayed active
/// forever. An overdue transaction now moves its subscription to past due.
/// </summary>
public sealed class SubscriptionOverdueTransactionHandlerTests
{
    [Fact]
    public async Task BecameOverdueAsync_ForASubscriptionsTransaction_MarksItPastDue()
    {
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(m => m.GetByCheckoutSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Subscription { ItemId = "sub-1", Status = SubscriptionStatus.Active }]);

        var handler = new SubscriptionOverdueTransactionHandler(lifecycle.Object, manager.Object);

        await handler.BecameOverdueAsync(new Transaction { CheckoutSessionId = "session-1", Status = TransactionStatus.Outstanding }, TestContext.Current.CancellationToken);

        lifecycle.Verify(l => l.MarkPastDueAsync("sub-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The Pay Later transaction of a plan sign-up copies its checkout's reference, which names the plan
    /// (<c>Subscription</c>, the plan's content item id), not the subscription; the subscription is found by the checkout.
    /// </summary>
    [Fact]
    public async Task BecameOverdueAsync_ForAPlanSignUpsPayLaterTransaction_MarksTheSubscriptionPastDue()
    {
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        lifecycle.Setup(l => l.MarkPastDueAsync("plan-1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Subscription)null);
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(m => m.GetByCheckoutSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Subscription { ItemId = "sub-1", Status = SubscriptionStatus.Active }]);

        var handler = new SubscriptionOverdueTransactionHandler(lifecycle.Object, manager.Object);

        // The shape PayLaterTransactionCheckoutHandler records for a checkout started by SubscriptionsController.
        var transaction = new Transaction
        {
            ReferenceType = "Subscription",
            ReferenceId = "plan-1",
            CheckoutSessionId = "session-1",
            Status = TransactionStatus.Outstanding,
        };

        await handler.BecameOverdueAsync(transaction, TestContext.Current.CancellationToken);

        lifecycle.Verify(l => l.MarkPastDueAsync("sub-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BecameOverdueAsync_ForARenewalTransaction_MarksThatSubscriptionPastDue()
    {
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var handler = new SubscriptionOverdueTransactionHandler(lifecycle.Object, Mock.Of<ISubscriptionManager>());

        await handler.BecameOverdueAsync(new Transaction { ReferenceType = "Subscription", ReferenceId = "sub-2" }, TestContext.Current.CancellationToken);

        lifecycle.Verify(l => l.MarkPastDueAsync("sub-2", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BecameOverdueAsync_ForAnEndedSubscription_DoesNothing()
    {
        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var manager = new Mock<ISubscriptionManager>();
        manager.Setup(m => m.GetByCheckoutSessionAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Subscription { ItemId = "sub-1", Status = SubscriptionStatus.Canceled }]);

        var handler = new SubscriptionOverdueTransactionHandler(lifecycle.Object, manager.Object);

        await handler.BecameOverdueAsync(new Transaction { CheckoutSessionId = "session-1" }, TestContext.Current.CancellationToken);

        lifecycle.Verify(l => l.MarkPastDueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
