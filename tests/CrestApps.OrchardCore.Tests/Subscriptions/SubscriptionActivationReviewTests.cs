using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Pins how an agreement is priced and dated when the first cycle is not an ordinary one.
/// </summary>
public sealed class SubscriptionActivationReviewTests
{
    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A trial establishes the agreement without billing a cycle. It starts trialing, its first real cycle is
    /// due when the trial ends, and no cycle has been billed yet, so the customer still gets every cycle they
    /// were sold.
    /// </summary>
    [Fact]
    public async Task ATrial_StartsTrialing_WithTheFirstCycleDueWhenTheTrialEnds()
    {
        var store = new InMemorySubscriptionStore();
        var handler = CreateHandler(new InMemoryPaymentAttemptStore(CreateSettledAttempt(confirmedAmount: 0m)), store);

        await handler.CompletedAsync(CreateContext(trialDays: 14, firstCycleAmount: 20m));

        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(0, subscription.CyclesBilled);
        Assert.Equal(20m, subscription.Amount);
        Assert.Equal(_now.AddDays(14), subscription.CurrentPeriodEndUtc);
        Assert.Equal(_now.AddDays(14), subscription.TrialEndsUtc);
        Assert.Equal(_now.AddDays(14), subscription.NextBillingUtc);
    }

    /// <summary>
    /// The agreement's price is the plan's cycle amount, read from the lines. The first attempt only says what
    /// was taken for the first cycle, and a first-cycle coupon makes that smaller; copying it forward would
    /// grant the coupon on every renewal.
    /// </summary>
    [Fact]
    public async Task AFirstCycleDiscount_DoesNotBecomeTheRecurringAmount()
    {
        var store = new InMemorySubscriptionStore();
        var handler = CreateHandler(new InMemoryPaymentAttemptStore(CreateSettledAttempt(confirmedAmount: 15m)), store);

        await handler.CompletedAsync(CreateContext(trialDays: 0, firstCycleAmount: 15m));

        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(1, subscription.CyclesBilled);
        Assert.Equal(20m, subscription.Amount);
    }

    private static PaymentAttempt CreateSettledAttempt(decimal confirmedAmount)
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = "Stripe",
            ObligationId = CheckoutObligations.Recurring(new BillingDurationKey(DurationType.Month, 1)),
            State = PaymentAttemptState.Succeeded,
            ProviderReference = "sub_stripe_1",
            TransactionId = "pi_1",
            Currency = "USD",
            ConfirmedAmount = confirmedAmount,
        };

    private static CheckoutFlowCompletedContext CreateContext(int trialDays, decimal firstCycleAmount)
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

    private static SubscriptionActivationCheckoutHandler CreateHandler(InMemoryPaymentAttemptStore attemptStore, InMemorySubscriptionStore store)
    {
        var manager = new SubscriptionManager(store, [], NullLogger<CatalogManager<Subscription>>.Instance);

        return new SubscriptionActivationCheckoutHandler(
            attemptStore,
            manager,
            Mock.Of<IContentManager>(),
            [],
            new TestClock(_now),
            NullLogger<SubscriptionActivationCheckoutHandler>.Instance);
    }
}
