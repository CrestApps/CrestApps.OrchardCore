using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A module charges what a transaction owes to the card its owner kept, without the owner present, and needs to know
/// whether the money was taken, the card was declined, or the cardholder has to approve it, so it can fall back to the
/// invoice. The charge runs through the checkout like any payment, and a charge that is not taken is on the timeline.
/// </summary>
public sealed class TransactionChargeSavedPaymentMethodTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheChargeGoesThrough_RecordsThePaymentAndSucceeds()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.SettlesOnCompletion();

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Succeeded, result.ChargeStatus);
        Assert.Equal(TransactionStatus.Paid, result.Transaction.Status);
        Assert.Equal(TransactionEventType.PaymentRecorded, result.Event.Type);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Paid)], context.Events.StatusChanges);

        var started = Assert.Single(context.Started);

        Assert.Equal(TransactionsConstants.ReferenceTypes.Transaction, started.ReferenceType);
        Assert.Equal("transaction-1", started.ReferenceId);
        Assert.Equal("customer-1", started.OwnerId);

        var begun = Assert.Single(context.Begun);

        Assert.Equal("test", begun.ProviderKey);
        Assert.True(CheckoutPaymentDataKeys.IsSet(begun.ProviderData, CheckoutPaymentDataKeys.OffSession));
        Assert.Equal("pm_1", CheckoutPaymentDataKeys.GetValue(begun.ProviderData, CheckoutPaymentDataKeys.SavedPaymentMethodReference));

        context.PaymentMethods.Verify(store => store.MarkChargedAsync("card-1", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheProviderIsStillSettling_ReportsPendingWithoutAFailure()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Completion = new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Pending, result.ChargeStatus);
        Assert.DoesNotContain(result.Transaction.Events, evt => evt.Type == TransactionEventType.PaymentFailed);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheCardIsDeclined_RecordsTheFailedChargeAndReportsDeclined()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Outcome = new PaymentBeginOutcome
        {
            Succeeded = false,
            Declined = true,
            ErrorMessage = "The payment was declined.",
            ProviderErrorMessage = "Your card has insufficient funds.",
        };

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Declined, result.ChargeStatus);
        Assert.Equal(TransactionStatus.Outstanding, result.Transaction.Status);
        Assert.Equal(TransactionEventType.PaymentFailed, result.Event.Type);
        Assert.Equal("Your card has insufficient funds.", result.Event.Reason);
        Assert.Equal(100m, result.Event.Amount);
        Assert.Contains("Visa •••• 4242", result.Event.Message, StringComparison.Ordinal);
        Assert.Contains(result.Transaction.Events, evt => evt.Type == TransactionEventType.PaymentFailed);
        Assert.Equal(["The charge of a saved card was not accepted."], context.CanceledReasons);
        context.PaymentMethods.Verify(store => store.MarkChargedAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheCardholderMustApprove_ReportsRequiresAuthentication()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Outcome = new PaymentBeginOutcome
        {
            Succeeded = false,
            Declined = true,
            RequiresAuthentication = true,
            ProviderErrorMessage = "The bank asked the cardholder to approve this payment.",
        };

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.RequiresAuthentication, result.ChargeStatus);
        Assert.Equal(TransactionEventType.PaymentFailed, result.Event.Type);
        Assert.Equal(TransactionStatus.Outstanding, result.Transaction.Status);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheProviderCannotStart_ReportsFailed()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Outcome = PaymentBeginOutcome.Failure("The payment could not be started.");

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Failed, result.ChargeStatus);
        Assert.Equal(TransactionEventType.PaymentFailed, result.Event.Type);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhenTheCompletionIsRefused_ReportsDeclined()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Completion = new CheckoutCompletionResult
        {
            Status = CheckoutCompletionStatus.Failed,
            ErrorMessage = "The payment was not confirmed.",
        };

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Declined, result.ChargeStatus);
        Assert.Equal("The payment was not confirmed.", result.Event.Reason);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WithAnotherOwnersCard_IsRefusedWithoutCharging()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Card.OwnerId = "customer-2";

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.NotCharged, result.ChargeStatus);
        Assert.Empty(context.Started);
        Assert.Empty(context.Begun);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WithAnExpiredCard_IsRefusedWithoutCharging()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Card.PaymentMethod.ExpirationYear = 2025;

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.NotCharged, result.ChargeStatus);
        Assert.Empty(context.Begun);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_ForAPaidTransaction_IsRefusedWithoutCharging()
    {
        // Arrange
        var paid = CreateOutstanding();
        paid.Status = TransactionStatus.Paid;
        paid.AmountPaid = 100m;

        var context = new TestContextBuilder(paid);

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.NotCharged, result.ChargeStatus);
        Assert.Empty(context.Begun);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WhileAnEarlierChargeIsSettling_FinishesItInsteadOfChargingAgain()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        context.Previous = new CheckoutSession
        {
            SessionId = "earlier-session",
            Status = CheckoutSessionStatus.AwaitingProvider,
        };

        context.Completion = new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Pending };

        // Act
        var result = await context.Service.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.Pending, result.ChargeStatus);
        Assert.Equal(["earlier-session"], context.Completed);
        Assert.Empty(context.Started);
        Assert.Empty(context.Begun);
    }

    [Fact]
    public async Task ChargeSavedPaymentMethodAsync_WithoutTheCheckout_IsRefused()
    {
        // Arrange
        var context = new TestContextBuilder(CreateOutstanding());

        // Act
        var result = await context.ServiceWithoutCheckout.ChargeSavedPaymentMethodAsync("transaction-1", "card-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(SavedPaymentMethodChargeStatus.NotCharged, result.ChargeStatus);
    }

    private static Transaction CreateOutstanding()
        => new()
        {
            ItemId = "transaction-1",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Title = "Annual renewal",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

    private sealed class TestContextBuilder
    {
        private readonly Mock<ICheckoutEngine> _engine = new();
        private readonly Mock<ICheckoutSessionStore> _sessions = new();

        public TestContextBuilder(params Transaction[] transactions)
        {
            Store = new FakeTransactionStore(transactions);
            Manager = TransactionManagerFactory.Resolve(Store, Events);

            Card = new CustomerPaymentMethod
            {
                ItemId = "card-1",
                OwnerId = "customer-1",
                IsDefault = true,
                PaymentMethod = new SavedPaymentMethod
                {
                    ProviderKey = "test",
                    CustomerReference = "cus_1",
                    PaymentMethodReference = "pm_1",
                    Brand = "visa",
                    Last4 = "4242",
                    ExpirationMonth = 12,
                    ExpirationYear = 2030,
                },
            };

            PaymentMethods
                .Setup(store => store.FindByIdAsync("card-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Card);

            _sessions
                .Setup(store => store.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, "transaction-1", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Previous);

            _engine
                .Setup(engine => engine.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((StartCheckoutRequest request, CancellationToken _) =>
                {
                    Started.Add(request);

                    return new CheckoutSession
                    {
                        SessionId = "charge-session",
                        ReferenceType = request.ReferenceType,
                        ReferenceId = request.ReferenceId,
                        OwnerId = request.OwnerId,
                    };
                });

            _engine
                .Setup(engine => engine.BeginPaymentAsync(It.IsAny<string>(), It.IsAny<BeginPaymentOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, BeginPaymentOptions options, CancellationToken _) =>
                {
                    Begun.Add(options);

                    return Outcome;
                });

            _engine
                .Setup(engine => engine.TryCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string sessionId, CancellationToken _) =>
                {
                    Completed.Add(sessionId);

                    if (OnCompleted is not null)
                    {
                        await OnCompleted();
                    }

                    return Completion;
                });

            _engine
                .Setup(engine => engine.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string reason, CancellationToken _) =>
                {
                    CanceledReasons.Add(reason);

                    return new CheckoutCompletionResult { Status = CheckoutCompletionStatus.Canceled };
                });
        }

        public FakeTransactionStore Store { get; }

        public RecordingTransactionEventHandler Events { get; } = new();

        public ITransactionManager Manager { get; }

        public TestClock Clock { get; } = new(_now);

        public Mock<ISavedPaymentMethodStore> PaymentMethods { get; } = new();

        public CustomerPaymentMethod Card { get; }

        public CheckoutSession Previous { get; set; }

        public PaymentBeginOutcome Outcome { get; set; } = new() { Succeeded = true };

        public CheckoutCompletionResult Completion { get; set; } = new() { Status = CheckoutCompletionStatus.Completed };

        public Func<Task> OnCompleted { get; set; }

        public List<StartCheckoutRequest> Started { get; } = [];

        public List<BeginPaymentOptions> Begun { get; } = [];

        public List<string> Completed { get; } = [];

        public List<string> CanceledReasons { get; } = [];

        public DefaultTransactionCommandService Service
            => TransactionCommandServiceFactory.Create(
                Manager,
                Clock,
                new ServiceCollection()
                    .AddSingleton(_engine.Object)
                    .AddSingleton(_sessions.Object)
                    .AddSingleton(PaymentMethods.Object)
                    .BuildServiceProvider(),
                new LocalLock(NullLogger<LocalLock>.Instance));

        public DefaultTransactionCommandService ServiceWithoutCheckout
            => TransactionCommandServiceFactory.Create(Manager, Clock);

        // Completing the checkout settles the transaction, as the settlement handler does for a confirmed payment.
        public void SettlesOnCompletion()
            => OnCompleted = async () =>
            {
                var transaction = await Manager.FindByIdAsync("transaction-1");

                transaction.Events.Add(new TransactionEvent
                {
                    Id = "payment-1",
                    CreatedUtc = _now,
                    Type = TransactionEventType.PaymentRecorded,
                    Amount = transaction.OutstandingAmount,
                    Method = TransactionsConstants.SettlementMethods.Online,
                    PaymentAttemptId = "attempt-1",
                });

                transaction.ApplyPayment(transaction.OutstandingAmount, TransactionsConstants.SettlementMethods.Online, _now, "Settled.");

                await Manager.UpdateAsync(transaction, data: null);
            };
    }
}
