using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.PayLater.Handlers;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

public sealed class PayLaterTransactionCheckoutHandlerTests
{
    private static readonly DateTime _now = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletedAsync_RecordsOutstandingTransactionFromSucceededPayLaterAttempt()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 100m,
            ExpectedTaxAmount = 8m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 30);
        var context = CreateCompletedContext("session-1", referenceType: "order", obligationId: "ob-1");

        // Act
        await handler.CompletedAsync(context);

        // Assert
        var transaction = Assert.Single(store.Transactions);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Equal(PayLaterCheckoutPaymentProvider.ProcessorKey, transaction.Source);
        Assert.Equal(108m, transaction.TotalAmount);
        Assert.Equal(108m, transaction.OutstandingAmount);
        Assert.Equal("USD", transaction.Currency);
        Assert.Equal("Book", transaction.Title);
        Assert.Equal(_now.AddDays(30), transaction.DueUtc);
        Assert.Contains(transaction.Events, e => e.Type == TransactionEventType.Created);
    }

    [Fact]
    public async Task CompletedAsync_IsIdempotentAcrossMultipleCompletions()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 50m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 0);
        var context = CreateCompletedContext("session-1", referenceType: "order", obligationId: "ob-1");

        // Act
        await handler.CompletedAsync(context);
        await handler.CompletedAsync(context);

        // Assert
        Assert.Single(store.Transactions);
    }

    [Fact]
    public async Task CompletedAsync_SkipsSettlementCheckoutsForExistingTransactions()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 50m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 30);
        var context = CreateCompletedContext("session-1", referenceType: TransactionsConstants.ReferenceTypes.Transaction, obligationId: "ob-1");

        // Act
        await handler.CompletedAsync(context);

        // Assert
        Assert.Empty(store.Transactions);
    }

    [Fact]
    public async Task CompletedAsync_IgnoresNonPayLaterAndUnsucceededAttempts()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var otherProvider = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = "stripe",
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 50m,
        };

        var pendingPayLater = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-2",
            State = PaymentAttemptState.Pending,
            ExpectedAmount = 50m,
        };

        var handler = CreateHandler(store, netTermDays: 30, otherProvider, pendingPayLater);
        var context = CreateCompletedContext("session-1", referenceType: "order", obligationId: "ob-1");

        // Act
        await handler.CompletedAsync(context);

        // Assert
        Assert.Empty(store.Transactions);
    }

    [Fact]
    public async Task CompletedAsync_RecordsGuestTransactionWithStableOwnerAndContact()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "guest-session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 100m,
            ExpectedTaxAmount = 8m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 30);
        var context = CreateGuestCompletedContext("guest-session-1", obligationId: "ob-1", new CheckoutContactInfo
        {
            DisplayName = "Jane Guest",
            Email = "jane@example.com",
        });

        // Act
        await handler.CompletedAsync(context);

        // Assert
        var transaction = Assert.Single(store.Transactions);
        Assert.Equal(CustomerOwnerKind.Guest, transaction.OwnerKind);
        Assert.Equal("guest-session-1", transaction.OwnerId);
        Assert.Equal("Jane Guest", transaction.GuestContactName);
        Assert.Equal("jane@example.com", transaction.GuestContactEmail);
    }

    [Fact]
    public async Task CompletedAsync_ReusesTheSameGuestOwnerAcrossRetries()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var first = new PaymentAttempt
        {
            SessionId = "guest-session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-1",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 40m,
            Currency = "USD",
        };

        var second = new PaymentAttempt
        {
            SessionId = "guest-session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = "ob-2",
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 60m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, netTermDays: 0, first, second);
        var context = CreateGuestCompletedContext("guest-session-1", obligationId: "ob-1", guestContact: null);

        // Act
        await handler.CompletedAsync(context);

        // Assert
        Assert.Equal(2, store.Transactions.Count);
        var ownerIds = store.Transactions.Select(t => t.OwnerId).Distinct().ToArray();
        Assert.Single(ownerIds);
        Assert.Equal("guest-session-1", ownerIds[0]);
        Assert.All(store.Transactions, t => Assert.Equal(CustomerOwnerKind.Guest, t.OwnerKind));
    }

    /// <summary>
    /// A recurring Pay Later commitment has no gateway keeping its schedule, so the cycle it covers has to
    /// be written onto the ledger. Without it the debt would be invoiced once and never again.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_StampsTheBillingCycleOnARecurringObligation()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var interval = new BillingDurationKey(DurationType.Month, 1);
        var obligationId = CheckoutObligations.Recurring(interval);

        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = obligationId,
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 20m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 30);
        var context = CreateRecurringCompletedContext("session-1", obligationId, billingCycleLimit: 12);

        // Act
        await handler.CompletedAsync(context);

        // Assert
        var transaction = Assert.Single(store.Transactions);

        Assert.NotNull(transaction.Recurrence);
        Assert.Equal(DurationType.Month, transaction.Recurrence.DurationType);
        Assert.Equal(1, transaction.Recurrence.BillingDuration);
        Assert.Equal(1, transaction.Recurrence.CycleNumber);
        Assert.Equal(12, transaction.Recurrence.CycleLimit);
        Assert.Equal(_now, transaction.Recurrence.PeriodStartUtc);
        Assert.Equal(_now.AddMonths(1), transaction.Recurrence.PeriodEndUtc);
    }

    /// <summary>
    /// A one-time purchase must not acquire a schedule, or the renewal sweep would invoice the customer
    /// again for something they bought once.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_LeavesAOneTimeObligationWithoutARecurrence()
    {
        // Arrange
        var store = new FakeTransactionStore();
        var attempt = new PaymentAttempt
        {
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = CheckoutObligations.OneTime,
            State = PaymentAttemptState.Succeeded,
            ExpectedAmount = 100m,
            Currency = "USD",
        };

        var handler = CreateHandler(store, attempt, netTermDays: 30);
        var context = CreateRecurringCompletedContext("session-1", CheckoutObligations.OneTime, billingCycleLimit: null);

        // Act
        await handler.CompletedAsync(context);

        // Assert
        Assert.Null(Assert.Single(store.Transactions).Recurrence);
    }

    private static CheckoutFlowCompletedContext CreateRecurringCompletedContext(string sessionId, string obligationId, int? billingCycleLimit)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            OwnerId = "owner-1",
            ReferenceType = "order",
            ReferenceId = "ref-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "plan",
            Order = 1,
            BillingItems =
            [
                new BillingItem { ItemId = obligationId, Description = "Membership", Amount = 20m },
            ],
        });

        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            FirstRecurringPaymentAmount = 20m,
            DueNow = 20m,
            GrandTotal = 20m,
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
                        BillingCycleLimit = billingCycleLimit,
                    },
                }
            ],
        });

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }

    private static PayLaterTransactionCheckoutHandler CreateHandler(FakeTransactionStore store, int netTermDays, params PaymentAttempt[] attempts)
    {
        var attemptStore = new InMemoryPaymentAttemptStore(attempts);
        var manager = TransactionManagerFactory.Create(store);
        var siteService = SiteServiceFactory.Create(new PayLaterSettings { NetTermDays = netTermDays });

        return new PayLaterTransactionCheckoutHandler(
            attemptStore,
            manager,
            siteService,
            new TestClock(_now),
            NullLogger<PayLaterTransactionCheckoutHandler>.Instance,
            new PassThroughStringLocalizer<PayLaterTransactionCheckoutHandler>());
    }

    private static PayLaterTransactionCheckoutHandler CreateHandler(FakeTransactionStore store, PaymentAttempt attempt, int netTermDays)
        => CreateHandler(store, netTermDays, attempt);

    private static CheckoutFlowCompletedContext CreateCompletedContext(string sessionId, string referenceType, string obligationId)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            OwnerId = "owner-1",
            ReferenceType = referenceType,
            ReferenceId = "ref-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems =
            [
                new BillingItem { ItemId = obligationId, Description = "Book", Amount = 100m },
            ],
        });

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }

    private static CheckoutFlowCompletedContext CreateGuestCompletedContext(string sessionId, string obligationId, CheckoutContactInfo guestContact)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            OwnerId = null,
            ReferenceType = "order",
            ReferenceId = "ref-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = "goods",
            Order = 1,
            BillingItems =
            [
                new BillingItem { ItemId = obligationId, Description = "Book", Amount = 100m },
            ],
        });

        if (guestContact is not null)
        {
            session.Put(guestContact);
        }

        return new CheckoutFlowCompletedContext(new CheckoutFlow(session));
    }
}
