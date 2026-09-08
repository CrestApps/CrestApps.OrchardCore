using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The handler is the seam between buying something once and owing it from now on. These tests pin what it
/// must never do: create an agreement the customer did not pay for, and create two agreements when one
/// checkout completes twice.
/// </summary>
public sealed class SubscriptionActivationCheckoutHandlerTests
{
    private static readonly DateTime _now = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletedAsync_CreatesTheAgreementFromTheSettledRecurringAttempt()
    {
        // Arrange
        var interval = new BillingDurationKey(DurationType.Month, 1);
        var store = new InMemorySubscriptionStore();
        var attemptStore = new InMemoryPaymentAttemptStore(CreateSettledAttempt(CheckoutObligations.Recurring(interval)));
        var handler = CreateHandler(attemptStore, store);

        // Act
        await handler.CompletedAsync(CreateContext());

        // Assert
        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("Membership", subscription.Title);
        Assert.Equal("owner-1", subscription.OwnerId);
        Assert.Equal(CustomerOwnerKind.Authenticated, subscription.OwnerKind);
        Assert.Equal("Stripe", subscription.ProviderKey);
        Assert.Equal("sub_stripe_1", subscription.ProviderSubscriptionId);
        // The cycle amount is read from the lines, not from the first attempt: an attempt reflects a
        // first-cycle discount or a trial, and an agreement that copied it would renew at that forever.
        Assert.Equal(20m, subscription.Amount);
        Assert.Equal(0m, subscription.TaxAmount);
        Assert.Equal(DurationType.Month, subscription.DurationType);
        Assert.Equal(1, subscription.BillingDuration);
        Assert.Equal(1, subscription.CyclesBilled);
        Assert.Equal(_now, subscription.CurrentPeriodStartUtc);
        Assert.Equal(_now.AddMonths(1), subscription.CurrentPeriodEndUtc);
        Assert.Equal(_now.AddMonths(1), subscription.NextBillingUtc);
        Assert.Equal("membership", Assert.Single(subscription.Lines).ItemId);
    }

    /// <summary>
    /// A checkout can complete more than once when a webhook and the reconciliation sweep both finish it.
    /// Creating a second agreement would double what the customer is billed from then on.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_Twice_CreatesOneAgreement()
    {
        // Arrange
        var interval = new BillingDurationKey(DurationType.Month, 1);
        var store = new InMemorySubscriptionStore();
        var attemptStore = new InMemoryPaymentAttemptStore(CreateSettledAttempt(CheckoutObligations.Recurring(interval)));
        var handler = CreateHandler(attemptStore, store);
        var context = CreateContext();

        // Act
        await handler.CompletedAsync(context);
        await handler.CompletedAsync(context);

        // Assert
        Assert.Single(store.Subscriptions);
    }

    /// <summary>
    /// An attempt that never settled means the customer never paid. Activating on it would hand out what
    /// they did not buy.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_WhenTheRecurringAttemptDidNotSettle_CreatesNothing()
    {
        // Arrange
        var interval = new BillingDurationKey(DurationType.Month, 1);
        var attempt = CreateSettledAttempt(CheckoutObligations.Recurring(interval));

        attempt.State = PaymentAttemptState.Failed;

        var store = new InMemorySubscriptionStore();
        var handler = CreateHandler(new InMemoryPaymentAttemptStore(attempt), store);

        // Act
        await handler.CompletedAsync(CreateContext());

        // Assert
        Assert.Empty(store.Subscriptions);
    }

    /// <summary>
    /// A one-time purchase is not an agreement, so it must not produce a subscription that renews.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_ForAOneTimeCheckout_CreatesNothing()
    {
        // Arrange
        var store = new InMemorySubscriptionStore();
        var attemptStore = new InMemoryPaymentAttemptStore(CreateSettledAttempt(CheckoutObligations.OneTime));
        var handler = CreateHandler(attemptStore, store);

        // Act
        await handler.CompletedAsync(CreateContext(recurring: false));

        // Assert
        Assert.Empty(store.Subscriptions);
    }

    /// <summary>
    /// A guest has no account, so the agreement carries the contact details captured at checkout. Without
    /// them a guest subscriber could never be reached about a failed renewal.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_ForAGuest_CarriesTheContactDetails()
    {
        // Arrange
        var interval = new BillingDurationKey(DurationType.Month, 1);
        var store = new InMemorySubscriptionStore();
        var attemptStore = new InMemoryPaymentAttemptStore(CreateSettledAttempt(CheckoutObligations.Recurring(interval)));
        var handler = CreateHandler(attemptStore, store);

        var context = CreateContext(ownerId: null);

        ((CheckoutSession)context.Flow.Session).Put(new CheckoutContactInfo
        {
            DisplayName = "Ada Lovelace",
            Email = "ada@example.com",
        });

        // Act
        await handler.CompletedAsync(context);

        // Assert
        var subscription = Assert.Single(store.Subscriptions);

        Assert.Equal(CustomerOwnerKind.Guest, subscription.OwnerKind);
        Assert.Equal("session-1", subscription.OwnerId);
        Assert.Equal("ada@example.com", subscription.GuestContactEmail);
    }

    private static PaymentAttempt CreateSettledAttempt(string obligationId)
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = "Stripe",
            ObligationId = obligationId,
            State = PaymentAttemptState.Succeeded,
            ProviderReference = "sub_stripe_1",
            TransactionId = "pi_1",
            Currency = "USD",
            ConfirmedAmount = 20m,
            ConfirmedTaxAmount = 2m,
        };

    private static CheckoutFlowCompletedContext CreateContext(bool recurring = true, string ownerId = "owner-1")
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            OwnerId = ownerId,
            ReferenceType = "SubscriptionPlan",
            ReferenceId = "plan-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            FirstRecurringPaymentAmount = recurring ? 22m : null,
            DueNow = 22m,
            GrandTotal = 22m,
            LineItems =
            [
                new CheckoutLineItem
                {
                    ItemId = "membership",
                    Description = "Membership",
                    Quantity = 1,
                    UnitPrice = 20m,
                    Plan = recurring
                        ? new RecurringPlan { DurationType = DurationType.Month, BillingDuration = 1 }
                        : null,
                }
            ],
        });

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }

    private static SubscriptionActivationCheckoutHandler CreateHandler(InMemoryPaymentAttemptStore attemptStore, InMemorySubscriptionStore store)
    {
        var manager = new SubscriptionManager(
            store,
            [],
            NullLogger<CatalogManager<Subscription>>.Instance);

        return new SubscriptionActivationCheckoutHandler(
            attemptStore,
            manager,
            Mock.Of<IContentManager>(),
            [],
            new TestClock(_now),
            NullLogger<SubscriptionActivationCheckoutHandler>.Instance);
    }
}
