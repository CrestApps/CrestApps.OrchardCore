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
