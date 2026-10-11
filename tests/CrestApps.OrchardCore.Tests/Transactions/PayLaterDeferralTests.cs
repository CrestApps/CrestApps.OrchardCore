using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Pins how an offline agreement's debts are priced when the first cycle is not an ordinary one.
/// </summary>
public sealed class PayLaterDeferralTests
{
    private static readonly DateTime _now = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A trial owes nothing until it ends, so the first debt is the first real cycle: it starts when the
    /// trial ends, for the full cycle amount. Recording a zero debt now and the real one never would give the
    /// plan away.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_WithATrial_RecordsTheFirstRealCycleWhenTheTrialEnds()
    {
        var store = new FakeTransactionStore();
        var obligationId = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1));

        var handler = CreateHandler(store, netTermDays: 0, new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = obligationId,
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 0m,
            Currency = "USD",
        });

        await handler.CompletedAsync(CreateRecurringContext(obligationId, trialDays: 14, firstCycleAmount: 0m));

        var transaction = Assert.Single(store.Transactions);

        Assert.Equal(20m, transaction.Amount);
        Assert.Equal(20m, transaction.TotalAmount);
        Assert.Equal(_now.AddDays(14), transaction.DueUtc);
        Assert.Equal(_now.AddDays(14), transaction.Recurrence.PeriodStartUtc);
        Assert.Equal(_now.AddDays(14).AddMonths(1), transaction.Recurrence.PeriodEndUtc);
        Assert.Equal(20m, transaction.Recurrence.CycleAmount);
    }

    /// <summary>
    /// A first-cycle discount is owed on the first debt only. The recurrence carries the full cycle amount
    /// so the next debt is not priced from the discounted one.
    /// </summary>
    [Fact]
    public async Task ADiscountedFirstCycle_DoesNotDiscountTheNextOne()
    {
        var store = new FakeTransactionStore();
        var obligationId = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1));

        var handler = CreateHandler(store, netTermDays: 0, new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = obligationId,
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 15m,
            Currency = "USD",
        });

        await handler.CompletedAsync(CreateRecurringContext(obligationId, trialDays: 0, firstCycleAmount: 15m));

        var first = Assert.Single(store.Transactions);
        Assert.Equal(15m, first.Amount);
        Assert.Equal(20m, first.Recurrence.CycleAmount);

        // Time passes; the renewal sweep invoices the next cycle.
        first.Status = TransactionStatus.Paid;
        first.AmountPaid = first.TotalAmount;

        var (task, services) = CreateRenewalTask(store, _now.AddMonths(1).AddDays(1));

        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        var next = Assert.Single(store.Transactions, t => t.Recurrence.CycleNumber == 2);

        Assert.Equal(20m, next.Amount);
        Assert.Equal(20m, next.TotalAmount);
        Assert.Equal(20m, next.Recurrence.CycleAmount);
    }

    private static CheckoutFlowCompletedContext CreateRecurringContext(string obligationId, int trialDays, decimal firstCycleAmount)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = "owner-1",
            ReferenceType = "Subscription",
            ReferenceId = "plan-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.PaymentPending,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "plan",
            Order = 1,
            BillingItems = [new BillingItem { ItemId = obligationId, Description = "Membership", Amount = 20m }],
        });

        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            FirstRecurringPaymentAmount = trialDays > 0 ? null : firstCycleAmount,
            DueNow = trialDays > 0 ? 0m : firstCycleAmount,
            GrandTotal = trialDays > 0 ? 0m : firstCycleAmount,
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "membership",
                    Description = "Membership",
                    Quantity = 1,
                    UnitPrice = 20m,
                    Plan = new RecurringPlan
                    {
                        DurationType = DurationType.Month,
                        BillingDuration = 1,
                        TrialDays = trialDays > 0 ? trialDays : null,
                    },
                },
            ],
        });

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }

    private static PayLaterTransactionCheckoutHandler CreateHandler(FakeTransactionStore store, int netTermDays, params PaymentAttempt[] attempts)
        => new(
            new InMemoryPaymentAttemptStore(attempts),
            TransactionManagerFactory.Create(store),
            SiteServiceFactory.Create(new PayLaterSettings { NetTermDays = netTermDays }),
            new TestClock(_now),
            NullLogger<PayLaterTransactionCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<PayLaterTransactionCheckoutHandler>());

    private static (PayLaterRenewalBackgroundTask Task, IServiceProvider Services) CreateRenewalTask(FakeTransactionStore store, DateTime now)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store));
        services.AddSingleton(SiteServiceFactory.Create(new PayLaterSettings { NetTermDays = 0 }));
        services.AddSingleton<IDistributedLock, LocalLock>();
        services.AddSingleton<IClock>(new TestClock(now));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        return (new PayLaterRenewalBackgroundTask(NullLogger<PayLaterRenewalBackgroundTask>.Instance), services.BuildServiceProvider());
    }
}
