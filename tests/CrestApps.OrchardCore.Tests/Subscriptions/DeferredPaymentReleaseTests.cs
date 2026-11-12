using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C5: what was bought with a promise to pay is held back until the money arrives. These pin the two handlers that
/// release it when its transaction is paid: the site waiting to be created, and the order whose downloads and
/// memberships wait.
/// </summary>
public sealed class DeferredPaymentReleaseTests
{
    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ASiteAwaitingPayment_BecomesDue_WhenItsTransactionIsPaid()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.AwaitingPayment,
        });
        var handler = new TenantProvisioningPaymentReceivedHandler(store, new TestClock(_now));

        // Act: a payment toward it that does not settle it yet changes nothing.
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1" }, TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid, TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, Assert.Single(store.Jobs).Status);

        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1" }, TransactionStatus.PartiallyPaid, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        var job = Assert.Single(store.Jobs);
        Assert.Equal(TenantProvisioningStatus.Pending, job.Status);
        Assert.Null(job.NextAttemptUtc);
    }

    [Fact]
    public async Task ASiteThatIsNotAwaitingPayment_IsLeftAlone()
    {
        // Arrange
        var store = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.Succeeded,
        });
        var handler = new TenantProvisioningPaymentReceivedHandler(store, new TestClock(_now));

        // Act
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1" }, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TenantProvisioningStatus.Succeeded, Assert.Single(store.Jobs).Status);
    }

    [Fact]
    public async Task AnOrderPaidLater_DeliversWhatItHeldBack_WhenItsTransactionIsPaid_Once()
    {
        // Arrange
        var order = new Order { ItemId = "order-1", Status = OrderStatus.Paid, PaymentDeferred = true };
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var settled = new List<string>();
        var orderHandler = new Mock<IOrderEventHandler>();
        orderHandler.Setup(value => value.PaymentSettledAsync(It.IsAny<OrderPaidContext>(), It.IsAny<CancellationToken>()))
            .Callback((OrderPaidContext context, CancellationToken _) => settled.Add(context.Order.ItemId))
            .Returns(Task.CompletedTask);

        var handler = new OrderPaymentSettledTransactionHandler(orders.Object, [orderHandler.Object], new TestClock(_now), NullLogger<OrderPaymentSettledTransactionHandler>.Instance);
        var transaction = new Transaction { ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" };

        Assert.False(order.IsPaymentCollected);

        // Act
        await handler.StatusChangedAsync(transaction, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);
        await handler.StatusChangedAsync(transaction, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(order.IsPaymentCollected);
        Assert.Equal(_now, order.PaymentSettledUtc);
        Assert.Equal(["order-1"], settled);
    }
}
