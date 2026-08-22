using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutReconciliationServiceTests
{
    private const string ProviderKey = "test-provider";
    private const string SessionId = "session-1";

    [Fact]
    public async Task ReconcileAsync_WhenProviderConfirmsSuccess_RecordsPaymentAndSettles()
    {
        // Arrange
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "txn-1",
            ReportsAuthoritativeAmount = true,
            Amount = 42m,
            Currency = "USD",
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFullySettled);
        Assert.Contains("obligation-1", result.SettledObligationIds);
        Assert.Equal(PaymentAttemptState.Succeeded, attempt.State);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.True(metadata.Payments.ContainsKey("txn-1"));
        Assert.Equal(42m, metadata.Payments["txn-1"].Amount);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderStatusUnknown_LeavesObligationOutstanding()
    {
        // Arrange: this is the orphan-prevention guarantee. A pending attempt whose provider cannot yet
        // confirm success must never be treated as paid.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Unknown,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
        Assert.Empty(result.SettledObligationIds);
        Assert.Equal(PaymentAttemptState.Pending, attempt.State);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderReportsFailure_ObligationFailsAndIsNotSettled()
    {
        // Arrange
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Failed,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.FailedObligationIds);
        Assert.Equal(PaymentAttemptState.Failed, attempt.State);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Empty(metadata.Payments);
    }

    [Fact]
    public async Task ReconcileAsync_WhenOneOfTwoObligationsOutstanding_IsNotFullySettled()
    {
        // Arrange: partial multi-obligation failure must never report the whole checkout as paid.
        var settled = NewAttempt("obligation-1", PaymentAttemptState.Succeeded);
        settled.TransactionId = "txn-1";

        var pending = NewAttempt("obligation-2", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(settled, pending);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Unknown,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1", "obligation-2"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.SettledObligationIds);
        Assert.Contains("obligation-2", result.OutstandingObligationIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderNotRegistered_LeavesObligationOutstanding()
    {
        // Arrange
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        attempt.ProviderKey = "missing-provider";
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
        Assert.Equal(0, provider.VerifyCallCount);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderReportsUnderpayment_DoesNotSettle()
    {
        // Arrange: a provider that claims success but charged less than the attempt expected must never
        // settle the obligation, otherwise an underpaid checkout would show as fully paid.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "txn-1",
            ReportsAuthoritativeAmount = true,
            Amount = 1m,
            Currency = "USD",
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
        Assert.NotEqual(PaymentAttemptState.Succeeded, attempt.State);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Empty(metadata.Payments);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderReportsWrongCurrency_DoesNotSettle()
    {
        // Arrange
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "txn-1",
            ReportsAuthoritativeAmount = true,
            Amount = 42m,
            Currency = "EUR",
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenProviderSucceedsWithoutTransactionId_DoesNotSettle()
    {
        // Arrange: a confirmation with no transaction id cannot be recorded, so it must never settle.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            ReportsAuthoritativeAmount = true,
            Amount = 42m,
            Currency = "USD",
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenDeferredProviderConfirms_SettlesWithoutAmountCrossCheck()
    {
        // Arrange: a deferred provider (for example Pay Later) never moves money at a processor, so it
        // reports success without an authoritative amount. The transaction id is enough to record it.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Succeeded,
            TransactionId = "deferred-1",
            ReportsAuthoritativeAmount = false,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFullySettled);
        Assert.Contains("obligation-1", result.SettledObligationIds);

        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Equal(42m, metadata.Payments["deferred-1"].Amount);
    }

    [Fact]
    public async Task ReconcileAsync_IsIdempotent_WhenRunTwice()
    {
        // Arrange: because the payment metadata is a pure projection of the durable ledger, reconciling
        // twice must not double-record or lose the confirmed payment.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Succeeded);
        attempt.TransactionId = "txn-1";
        attempt.ConfirmedAmount = 42m;
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Unknown,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFullySettled);
        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Single(metadata.Payments);
        Assert.Equal(0, provider.VerifyCallCount);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAttemptOmittedFromExpectedSet_IsStillEvaluated()
    {
        // Arrange: an outstanding attempt that the caller forgot to list must not be silently dropped and
        // reported as fully settled.
        var pending = NewAttempt("obligation-hidden", PaymentAttemptState.Pending);
        var store = new InMemoryPaymentAttemptStore(pending);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Unknown,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act: the expected set is empty, yet the durable attempt must still be reconciled.
        var result = await service.ReconcileAsync(session, [], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-hidden", result.OutstandingObligationIds);
    }

    [Fact]
    public async Task ReconcileAsync_WhenSucceededAttemptHasNoTransactionId_DoesNotSettle()
    {
        // Arrange: a persisted "succeeded" attempt that carries no provider transaction id is inconsistent
        // and must never complete a checkout, because it would contribute no payment record.
        var attempt = NewAttempt("obligation-1", PaymentAttemptState.Succeeded);
        attempt.TransactionId = null;
        var store = new InMemoryPaymentAttemptStore(attempt);

        var provider = new FakeCheckoutPaymentProvider(ProviderKey, _ => new PaymentVerificationResult
        {
            Status = PaymentStatus.Unknown,
        });

        var service = CreateService(store, provider);
        var session = NewSession();

        // Act
        var result = await service.ReconcileAsync(session, ["obligation-1"], TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsFullySettled);
        Assert.Contains("obligation-1", result.OutstandingObligationIds);
        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.Empty(metadata.Payments);
    }

    private static CheckoutReconciliationService CreateService(
        IPaymentAttemptStore store,
        params ICheckoutPaymentProvider[] providers)
    {
        var resolver = new CheckoutPaymentProviderResolver(providers);

        return new CheckoutReconciliationService(store, resolver, NullLogger<CheckoutReconciliationService>.Instance);
    }

    private static PaymentAttempt NewAttempt(string obligationId, PaymentAttemptState state)
        => new()
        {
            ItemId = "attempt-" + obligationId,
            SessionId = SessionId,
            ProviderKey = ProviderKey,
            ObligationId = obligationId,
            IdempotencyKey = "idem-" + obligationId,
            ExpectedAmount = 42m,
            Currency = "USD",
            State = state,
        };

    private static CheckoutSession NewSession()
        => new()
        {
            SessionId = SessionId,
            ReferenceType = "test",
            Currency = "USD",
        };
}
