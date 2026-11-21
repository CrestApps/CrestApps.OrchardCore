using CrestApps.Core.Services;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Pay Later has no gateway keeping a recurring agreement's schedule, so this sweep is the only thing that
/// invoices the next period. These tests pin the two behaviors that matter: a period that has ended is
/// billed, and no period is ever billed twice.
/// </summary>
public sealed class PayLaterRenewalBackgroundTaskTests
{
    private static readonly DateTime _now = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DoWorkAsync_WhenTheBillingPeriodHasEnded_CreatesTheNextCycle()
    {
        // Arrange
        var store = new FakeTransactionStore();
        await store.CreateAsync(CreateRecurringTransaction(periodEndUtc: _now.AddDays(-1)), TestContext.Current.CancellationToken);

        var (task, services) = CreateTask(store);

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, store.Transactions.Count);

        var next = store.Transactions.Single(t => t.Recurrence.CycleNumber == 2);

        Assert.Equal(TransactionStatus.Outstanding, next.Status);
        Assert.Equal(50m, next.Amount);
        Assert.Equal(54m, next.TotalAmount);
        Assert.Equal(0m, next.AmountPaid);
        Assert.Equal("ob-1:cycle-2", next.ObligationId);
        Assert.Equal(_now.AddDays(-1), next.Recurrence.PeriodStartUtc);
        Assert.Equal(_now.AddDays(-1).AddMonths(1), next.Recurrence.PeriodEndUtc);
        Assert.Equal(_now.AddDays(-1).AddDays(30), next.DueUtc);
    }

    /// <summary>
    /// Running the sweep twice must not invoice the same period twice. The source's spawned flag is what
    /// makes that true, and it is exactly what a retry or a second node would race on.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_RunTwice_CreatesEachCycleOnce()
    {
        // Arrange
        var store = new FakeTransactionStore();
        await store.CreateAsync(CreateRecurringTransaction(periodEndUtc: _now.AddDays(-1)), TestContext.Current.CancellationToken);

        var (task, services) = CreateTask(store);

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, store.Transactions.Count);
    }

    /// <summary>
    /// A period that has not ended yet is not billed early, and an agreement that reached its agreed number
    /// of cycles stops rather than billing forever.
    /// </summary>
    [Theory]
    [InlineData(30, 12)]
    [InlineData(-1, 1)]
    public async Task DoWorkAsync_WhenTheAgreementIsNotDue_CreatesNothing(int periodEndOffsetDays, int cycleLimit)
    {
        // Arrange
        var store = new FakeTransactionStore();

        var transaction = CreateRecurringTransaction(periodEndUtc: _now.AddDays(periodEndOffsetDays));
        transaction.Recurrence.CycleLimit = cycleLimit;

        await store.CreateAsync(transaction, TestContext.Current.CancellationToken);

        var (task, services) = CreateTask(store);

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(store.Transactions);
    }

    /// <summary>
    /// A canceled agreement stops billing. Continuing to invoice someone who canceled is the worst failure
    /// this sweep could have.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_WhenTheAgreementIsCanceled_CreatesNothing()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var transaction = CreateRecurringTransaction(periodEndUtc: _now.AddDays(-1));
        transaction.Recurrence.Canceled = true;

        await store.CreateAsync(transaction, TestContext.Current.CancellationToken);

        var (task, services) = CreateTask(store);

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(store.Transactions);
    }

    /// <summary>
    /// SEC-C5: with no payment terms (<c>NetTermDays = 0</c>) a renewal had no due date, and a transaction without one never
    /// becomes overdue, so after the first payment the agreement never lapsed. It is now due on the first day of the
    /// period it bills.
    /// </summary>
    [Fact]
    public async Task DoWorkAsync_WithNoPaymentTerms_DatesTheNextCycleOnItsFirstDay()
    {
        // Arrange
        var store = new FakeTransactionStore();
        await store.CreateAsync(CreateRecurringTransaction(periodEndUtc: _now.AddDays(-1)), TestContext.Current.CancellationToken);

        var (task, services) = CreateTask(store, netTermDays: 0);

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        var next = store.Transactions.Single(t => t.Recurrence.CycleNumber == 2);

        Assert.Equal(_now.AddDays(-1), next.DueUtc);
    }

    /// <summary>
    /// SEC-C5: the sweep asked nothing about the subscription a transaction bills, so one still waiting for its first
    /// payment (incomplete) was invoiced again every cycle, and a canceled one kept being invoiced. A subscription that
    /// waits holds the schedule; one that ended stops it.
    /// </summary>
    [Theory]
    [InlineData(SubscriptionStatus.Incomplete, false, false, false)]
    [InlineData(SubscriptionStatus.Paused, false, false, false)]
    [InlineData(SubscriptionStatus.Canceled, false, false, true)]
    [InlineData(SubscriptionStatus.Expired, false, false, true)]
    [InlineData(SubscriptionStatus.Active, true, false, true)]
    [InlineData(SubscriptionStatus.Active, false, true, false)]
    [InlineData(SubscriptionStatus.PastDue, false, true, false)]
    public async Task DoWorkAsync_FollowsTheSubscriptionItBills(SubscriptionStatus status, bool cancelAtPeriodEnd, bool renews, bool stops)
    {
        // Arrange
        var store = new FakeTransactionStore();
        await store.CreateAsync(CreateRecurringTransaction(periodEndUtc: _now.AddDays(-1)), TestContext.Current.CancellationToken);

        var subscriptions = new InMemorySubscriptionStore(new Subscription
        {
            ItemId = "subscription-1",
            CheckoutSessionId = "session-1",
            ObligationId = "ob-1",
            Status = status,
            CancelAtPeriodEnd = cancelAtPeriodEnd,
        });
        var manager = new SubscriptionManager(subscriptions, [], NullLogger<CatalogManager<Subscription>>.Instance);

        var (task, services) = CreateTask(store, filter: new SubscriptionTransactionRenewalFilter(manager));

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(renews ? 2 : 1, store.Transactions.Count);
        Assert.Equal(stops, store.Transactions.Single(t => t.ItemId == "transaction-1").Recurrence.Canceled);
    }

    private static Transaction CreateRecurringTransaction(DateTime periodEndUtc)
        => new()
        {
            ItemId = "transaction-1",
            Title = "Membership",
            Source = PayLaterCheckoutPaymentProvider.ProcessorKey,
            OwnerId = "owner-1",
            ReferenceType = "order",
            ReferenceId = "ref-1",
            CheckoutSessionId = "session-1",
            ObligationId = "ob-1",
            Currency = "USD",
            Amount = 50m,
            TaxAmount = 4m,
            TotalAmount = 54m,
            AmountPaid = 54m,
            Status = TransactionStatus.Paid,
            CreatedUtc = periodEndUtc.AddMonths(-1),
            UpdatedUtc = periodEndUtc.AddMonths(-1),
            Recurrence = new TransactionRecurrence
            {
                BillingDuration = 1,
                DurationType = DurationType.Month,
                PeriodStartUtc = periodEndUtc.AddMonths(-1),
                PeriodEndUtc = periodEndUtc,
                CycleNumber = 1,
                CycleLimit = 12,
                CycleAmount = 50m,
                CycleTaxAmount = 4m,
            },
        };

    private static (PayLaterRenewalBackgroundTask Task, IServiceProvider Services) CreateTask(FakeTransactionStore store, int netTermDays = 30, ITransactionRenewalFilter filter = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        if (filter is not null)
        {
            services.AddSingleton(filter);
        }

        services.AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store));
        services.AddSingleton(SiteServiceFactory.Create(new PayLaterSettings { NetTermDays = netTermDays }));
        services.AddSingleton<IDistributedLock, LocalLock>();
        services.AddSingleton<IClock>(new TestClock(_now));
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).Returns<DateTimeOffset>(Task.FromResult);
        services.AddSingleton(localClock.Object);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        var task = new PayLaterRenewalBackgroundTask(NullLogger<PayLaterRenewalBackgroundTask>.Instance);

        return (task, services.BuildServiceProvider());
    }
}
