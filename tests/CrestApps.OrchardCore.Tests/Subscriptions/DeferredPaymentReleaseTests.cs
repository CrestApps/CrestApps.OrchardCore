using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Transactions;
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
        var handler = CreateTenantHandler(store);
        var transaction = new Transaction { CheckoutSessionId = "session-1", ObligationId = CheckoutObligations.OneTime };

        // Act: a payment toward it that does not settle it yet changes nothing.
        await handler.StatusChangedAsync(transaction, TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid, TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, Assert.Single(store.Jobs).Status);

        await handler.StatusChangedAsync(transaction, TransactionStatus.PartiallyPaid, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        var job = Assert.Single(store.Jobs);
        Assert.Equal(TenantProvisioningStatus.Pending, job.Status);
        Assert.Null(job.NextAttemptUtc);
    }

    /// <summary>
    /// SEC-C5: a site sold with a plan waits for the plan's own obligation. Pay Later records one transaction per
    /// obligation, so paying the checkout's small one-time balance must not create a site whose plan is unpaid.
    /// </summary>
    [Fact]
    public async Task ASiteSoldWithAPlan_WaitsForThePlansObligation_NotTheOneTimeBalance()
    {
        // Arrange
        var planObligation = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1));
        var store = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.AwaitingPayment,
        });
        var subscriptions = new InMemorySubscriptionStore(new Subscription
        {
            ItemId = "subscription-1",
            CheckoutSessionId = "session-1",
            ObligationId = planObligation,
            Status = SubscriptionStatus.Incomplete,
        });
        var handler = CreateTenantHandler(store, subscriptions);

        // Act: the one-time balance is paid.
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1", ObligationId = CheckoutObligations.OneTime }, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert: the site still waits.
        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, Assert.Single(store.Jobs).Status);

        // Act: the plan's first cycle is paid.
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1", ObligationId = planObligation }, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TenantProvisioningStatus.Pending, Assert.Single(store.Jobs).Status);
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
        var handler = CreateTenantHandler(store);

        // Act
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1", ObligationId = CheckoutObligations.OneTime }, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TenantProvisioningStatus.Succeeded, Assert.Single(store.Jobs).Status);
    }

    /// <summary>
    /// Round 2, #7: a Pay Later balance turned into installments cancels the site's transaction, and the installment
    /// payments carry no checkout of the site, so a buyer who paid every installment never got the site.
    /// </summary>
    [Fact]
    public async Task ASiteWhoseBalanceBecameAnInstallmentPlan_IsReleased_WhenTheLastInstallmentIsPaid()
    {
        // Arrange
        var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.AwaitingPayment,
        });
        var transactions = new FakeTransactionStore(new Transaction
        {
            ItemId = "source",
            CheckoutSessionId = "session-1",
            ObligationId = CheckoutObligations.OneTime,
            Status = TransactionStatus.Canceled,
        });
        var plans = new InMemoryInstallmentPlanStore();

        await plans.CreateAsync(new InstallmentPlan
        {
            ItemId = "plan-1",
            SourceTransactionId = "source",
            Status = InstallmentPlanStatus.Active,
            Payments =
            [
                new InstallmentPlanPayment { Number = 1, TransactionId = "installment-1", Status = InstallmentPaymentStatus.Paid },
                new InstallmentPlanPayment { Number = 2, TransactionId = "installment-2", Status = InstallmentPaymentStatus.Due },
            ],
        }, TestContext.Current.CancellationToken);

        var handler = CreateTenantHandler(jobs, transactions: transactions, plans: plans);

        Transaction Installment(string id) => new()
        {
            ItemId = id,
            ReferenceType = SubscriptionConstants.InstallmentPlans.ReferenceType,
            ReferenceId = "plan-1",
            Status = TransactionStatus.Paid,
        };

        // Act: an installment that isn't the last changes nothing.
        await handler.StatusChangedAsync(Installment("installment-1"), TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, Assert.Single(jobs.Jobs).Status);

        await handler.StatusChangedAsync(Installment("installment-2"), TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TenantProvisioningStatus.Pending, Assert.Single(jobs.Jobs).Status);
    }

    /// <summary>
    /// Round 2, #7: staff can ask for a waiting job to be checked again, but that releases only a site that is paid for.
    /// </summary>
    [Fact]
    public async Task CheckingAWaitingJobAgain_ReleasesOnlyAPaidSite()
    {
        // Arrange
        var job = new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.AwaitingPayment,
        };
        var jobs = new InMemoryTenantProvisioningJobStore(job);
        var transaction = new Transaction
        {
            ItemId = "site",
            CheckoutSessionId = "session-1",
            ObligationId = CheckoutObligations.OneTime,
            Status = TransactionStatus.Outstanding,
        };
        var release = CreateRelease(jobs, new FakeTransactionStore(transaction));

        // Act
        var whileUnpaid = await release.TryReleaseAsync(job, TestContext.Current.CancellationToken);

        transaction.Status = TransactionStatus.Paid;

        var oncePaid = await release.TryReleaseAsync(job, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(whileUnpaid);
        Assert.True(oncePaid);
        Assert.Equal(TenantProvisioningStatus.Pending, job.Status);
    }

    /// <summary>
    /// Round 3 (Medium): a payment that arrived after the site's subscription ended released the site anyway; the
    /// transition that ended the subscription had found no site to suspend, so the site then ran unbilled. The job is
    /// given up instead.
    /// </summary>
    [Theory]
    [InlineData(SubscriptionStatus.Expired)]
    [InlineData(SubscriptionStatus.Canceled)]
    public async Task ASiteWhoseSubscriptionEnded_IsGivenUp_WhenItsTransactionIsPaidLate(SubscriptionStatus ended)
    {
        // Arrange
        var planObligation = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1));
        var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            ProtectedAdminPassword = "protected",
            Status = TenantProvisioningStatus.AwaitingPayment,
        });
        var subscriptions = new InMemorySubscriptionStore(new Subscription
        {
            ItemId = "subscription-1",
            CheckoutSessionId = "session-1",
            ObligationId = planObligation,
            Status = ended,
            CurrentPeriodEndUtc = _now.AddDays(-1),
        });
        var handler = CreateTenantHandler(jobs, subscriptions);

        // Act
        await handler.StatusChangedAsync(new Transaction { CheckoutSessionId = "session-1", ObligationId = planObligation }, TransactionStatus.Outstanding, TransactionStatus.Paid, TestContext.Current.CancellationToken);

        // Assert
        var job = Assert.Single(jobs.Jobs);
        Assert.Equal(TenantProvisioningStatus.Abandoned, job.Status);
        Assert.Null(job.ProtectedAdminPassword);
        Assert.False(string.IsNullOrEmpty(job.LastError));
    }

    /// <summary>
    /// Round 3 (Medium): a site waiting for its payment holds its name until its subscription ends; then the job is given
    /// up, and the name is free again.
    /// </summary>
    [Fact]
    public async Task ASiteWaitingForPayment_IsGivenUp_WhenItsSubscriptionEnds()
    {
        // Arrange
        var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            CheckoutSessionId = "session-1",
            TenantName = "shop1",
            Status = TenantProvisioningStatus.AwaitingPayment,
        });
        var subscription = new Subscription
        {
            ItemId = "subscription-1",
            CheckoutSessionId = "session-1",
            Status = SubscriptionStatus.Incomplete,
            CurrentPeriodEndUtc = _now.AddMonths(1),
        };
        var handler = new TenantProvisioningSubscriptionEndedHandler(CreateRelease(jobs, subscriptions: new InMemorySubscriptionStore(subscription)));

        // Act: canceling at period end leaves paid time to run, so the site still waits.
        subscription.Status = SubscriptionStatus.Canceled;
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Incomplete));

        Assert.Equal(TenantProvisioningStatus.AwaitingPayment, Assert.Single(jobs.Jobs).Status);

        subscription.Status = SubscriptionStatus.Expired;
        subscription.CurrentPeriodEndUtc = _now;
        await handler.ChangedAsync(new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Canceled));

        // Assert
        Assert.Equal(TenantProvisioningStatus.Abandoned, Assert.Single(jobs.Jobs).Status);
    }

    private static TenantProvisioningPaymentReceivedHandler CreateTenantHandler(
        InMemoryTenantProvisioningJobStore jobs,
        InMemorySubscriptionStore subscriptions = null,
        FakeTransactionStore transactions = null,
        InMemoryInstallmentPlanStore plans = null)
        => new(CreateRelease(jobs, transactions, subscriptions, plans));

    private static TenantProvisioningPaymentRelease CreateRelease(
        InMemoryTenantProvisioningJobStore jobs,
        FakeTransactionStore transactions = null,
        InMemorySubscriptionStore subscriptions = null,
        InMemoryInstallmentPlanStore plans = null)
        => new(
            jobs,
            transactions ?? new FakeTransactionStore(),
            new SubscriptionManager(subscriptions ?? new InMemorySubscriptionStore(), [], NullLogger<CatalogManager<Subscription>>.Instance),
            plans is null ? [] : [plans],
            new TestClock(_now));

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
