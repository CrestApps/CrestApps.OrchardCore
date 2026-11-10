using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Core.Telemetry;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Telemetry.Commerce;

/// <summary>
/// The checkout engine reports its funnel on the <c>CrestApps.OrchardCore.Checkout</c> meter: how payments begin, what
/// reconciliation hears from the provider and how checkouts end, with ids on spans only. The names are a public
/// contract that dashboards and alerts depend on.
/// </summary>
public sealed class CheckoutTelemetryTests
{
    private const string Currency = "USD";

    [Fact]
    public async Task ACompletedCheckout_RecordsTheFunnelWithBoundedTags()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var providerKey = "telemetry-" + Guid.NewGuid().ToString("n");
        var sessionId = "session-" + Guid.NewGuid().ToString("n");
        var clock = new TestClock(DateTime.UtcNow);
        var (engine, sessionStore) = CreateEngine(new ConfirmingProvider(providerKey), clock);

        sessionStore.Seed(CreateSession(sessionId, clock.UtcNow.AddMinutes(-5)));

        // Act
        var begun = await engine.BeginPaymentAsync(sessionId, new BeginPaymentOptions { ProviderKey = providerKey }, TestContext.Current.CancellationToken);
        var completed = await engine.TryCompleteAsync(sessionId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(begun.Succeeded);
        Assert.Equal(CheckoutCompletionStatus.Completed, completed.Status);

        var paymentBegin = Assert.Single(capture.Find("crestapps.checkout.payment.begin", CrestAppsOrchardCoreTelemetry.ProviderTag, providerKey));
        Assert.Equal(CheckoutTelemetry.PaymentBeginOk, paymentBegin.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        var reconciled = Assert.Single(capture.Find("crestapps.checkout.reconcile.attempts", CrestAppsOrchardCoreTelemetry.ProviderTag, providerKey));
        Assert.Equal(CheckoutTelemetry.ReconcileSettled, reconciled.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);

        Assert.NotEmpty(capture.Find("crestapps.checkout.sessions", CrestAppsOrchardCoreTelemetry.OutcomeTag, CheckoutTelemetry.SessionCompleted));
        Assert.NotEmpty(capture.Find("crestapps.checkout.session.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, CheckoutTelemetry.SessionCompleted));
        Assert.Contains(
            capture.Find("crestapps.checkout.duration", CrestAppsOrchardCoreTelemetry.OperationTag, CheckoutTelemetry.OperationComplete),
            measurement => Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.ResultTag), "completed"));

        // The session id is on the spans, never on a metric.
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.checkout.complete" && Equals(span.GetTagItem(CheckoutTelemetry.SessionIdTag), sessionId));
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.checkout.provider.begin" && Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.ProviderTag), providerKey));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, sessionId)));
        AssertNoBannedTags(capture);
    }

    [Fact]
    public async Task ACanceledCheckout_RecordsTheCancellationAndTheResultOfEachCall()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var providerKey = "telemetry-" + Guid.NewGuid().ToString("n");
        var sessionId = "session-" + Guid.NewGuid().ToString("n");
        var clock = new TestClock(DateTime.UtcNow);
        var (engine, sessionStore) = CreateEngine(new ConfirmingProvider(providerKey), clock);

        sessionStore.Seed(CreateSession(sessionId, clock.UtcNow.AddMinutes(-1)));

        // Act
        await engine.CancelAsync(sessionId, "changed my mind", TestContext.Current.CancellationToken);
        await engine.CancelAsync(sessionId, "again", TestContext.Current.CancellationToken);

        // Assert
        var cancels = capture.Spans
            .Where(span => span.OperationName == "crestapps.checkout.cancel" && Equals(span.GetTagItem(CheckoutTelemetry.SessionIdTag), sessionId))
            .ToList();

        Assert.Equal(2, cancels.Count);
        Assert.All(cancels, span => Assert.Equal("cancelled", span.GetTagItem(CrestAppsOrchardCoreTelemetry.ResultTag)));
        Assert.NotEmpty(capture.Find("crestapps.checkout.sessions", CrestAppsOrchardCoreTelemetry.OutcomeTag, CheckoutTelemetry.SessionCancelled));
        AssertNoBannedTags(capture);
    }

    [Theory]
    [InlineData(CheckoutCompletionStatus.Completed, "completed")]
    [InlineData(CheckoutCompletionStatus.AlreadyCompleted, "already_completed")]
    [InlineData(CheckoutCompletionStatus.Pending, "pending")]
    [InlineData(CheckoutCompletionStatus.Blocked, "blocked")]
    [InlineData(CheckoutCompletionStatus.Failed, "failed")]
    [InlineData(CheckoutCompletionStatus.Canceled, "cancelled")]
    [InlineData(CheckoutCompletionStatus.NotFound, "not_found")]
    public void GetResult_MapsEveryStatusToABoundedValue(CheckoutCompletionStatus status, string expected)
    {
        // Act
        var result = CheckoutTelemetry.GetResult(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetProviderTag_IsUnknownUntilTheProviderIsResolved()
    {
        // Act and Assert
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Unknown, CheckoutTelemetry.GetProviderTag(null));
        Assert.Equal("stripe", CheckoutTelemetry.GetProviderTag(new ConfirmingProvider("stripe")));
    }

    private static void AssertNoBannedTags(TelemetryCapture capture)
    {
        var banned = capture.Measurements
            .Where(measurement => measurement.Meter == CheckoutTelemetry.SourceName)
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .ToList();

        Assert.Empty(banned);
    }

    private static CheckoutSession CreateSession(string sessionId, DateTime createdUtc)
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            Status = CheckoutSessionStatus.Pending,
            Currency = Currency,
            CreatedUtc = createdUtc,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
            CollectData = false,
        });

        session.Put(new CheckoutInvoice
        {
            Currency = Currency,
            InitialPaymentAmount = 30m,
            DueNow = 30m,
            GrandTotal = 30m,
            LineItems = [new CheckoutLineItem { ItemId = "one-time", Quantity = 1, UnitPrice = 30m }],
        });

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore SessionStore) CreateEngine(ConfirmingProvider provider, TestClock clock)
    {
        var sessionStore = new InMemoryCheckoutSessionStore();
        var attemptStore = new InMemoryPaymentAttemptStore();

        var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
        providerResolver.Setup(resolver => resolver.GetProvider(provider.Key)).Returns(provider);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(locker => locker.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((new NoopLocker(), true));

        var engine = new DefaultCheckoutEngine(
            sessionStore,
            attemptStore,
            providerResolver.Object,
            new Mock<ICheckoutRecurringPaymentProviderResolver>().Object,
            new CheckoutReconciliationService(attemptStore, providerResolver.Object, NullLogger<CheckoutReconciliationService>.Instance),
            new Mock<ICheckoutRefundService>().Object,
            [],
            distributedLock.Object,
            new Mock<ISession>().Object,
            clock,
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessionStore);
    }

    private sealed class ConfirmingProvider : ICheckoutPaymentProvider
    {
        public ConfirmingProvider(string key)
            => Key = key;

        public string Key { get; }

        public string DisplayName => Key;

        public PaymentProviderCapabilities Capabilities { get; } = new()
        {
            SupportsOneTimePayments = true,
        };

        public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(PaymentBeginResult.Success("provider-ref"));

        public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new PaymentVerificationResult
            {
                Status = PaymentStatus.Succeeded,
                TransactionId = "txn-" + context.Attempt.ObligationId,
                ReportsAuthoritativeAmount = true,
                Amount = context.Attempt.ExpectedAmount,
                Currency = Currency,
            });

        public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(PaymentCancelResult.Success());
    }

    private sealed class NoopLocker : ILocker
    {
        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}
