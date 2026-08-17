using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Checkout;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.PayLater;

public sealed class PayLaterCheckoutPaymentProviderTests
{
    [Fact]
    public async Task VerifyAsync_ReportsSuccessWithoutAuthoritativeAmount()
    {
        // Arrange
        var provider = CreateProvider(isProduction: false);
        var attempt = new PaymentAttempt
        {
            Id = "attempt-1",
            ProviderReference = "ref-1",
            ExpectedAmount = 42m,
            ExpectedTaxAmount = 2m,
            Currency = "USD",
        };

        // Act
        var result = await provider.VerifyAsync(
            new VerifyPaymentContext { Attempt = attempt },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.False(result.ReportsAuthoritativeAmount);
        Assert.Equal("ref-1", result.TransactionId);
        Assert.Equal(42m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(GatewayMode.Testing, result.GatewayMode);
    }

    [Fact]
    public async Task VerifyAsync_InProduction_ReportsLiveGatewayMode()
    {
        // Arrange
        var provider = CreateProvider(isProduction: true);
        var attempt = new PaymentAttempt { Id = "attempt-1", ProviderReference = "ref-1", Currency = "USD" };

        // Act
        var result = await provider.VerifyAsync(
            new VerifyPaymentContext { Attempt = attempt },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(GatewayMode.Live, result.GatewayMode);
    }

    [Fact]
    public async Task Reconciliation_SettlesPayLaterAttempt_WithoutAmountCrossCheck()
    {
        // Arrange: a deferred provider must settle on the strength of its transaction id alone, so an
        // arbitrary expected amount is accepted without a processor cross-check.
        var provider = CreateProvider(isProduction: false);

        var attempt = new PaymentAttempt
        {
            Id = "attempt-1",
            SessionId = "session-1",
            ProviderKey = PayLaterCheckoutPaymentProvider.ProcessorKey,
            ObligationId = CheckoutObligations.OneTime,
            ProviderReference = "ref-1",
            ExpectedAmount = 99m,
            Currency = "USD",
            State = PaymentAttemptState.Pending,
        };

        var store = new InMemoryPaymentAttemptStore(attempt);
        var resolver = new CheckoutPaymentProviderResolver([provider]);
        var reconciliation = new CheckoutReconciliationService(store, resolver, NullLogger<CheckoutReconciliationService>.Instance);

        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };

        // Act
        var result = await reconciliation.ReconcileAsync(session, [CheckoutObligations.OneTime], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFullySettled);
        Assert.Equal(PaymentAttemptState.Succeeded, attempt.State);
        Assert.True(session.TryGet<PaymentsMetadata>(out var metadata));
        Assert.True(metadata.Payments.ContainsKey("ref-1"));
        Assert.Equal(99m, metadata.Payments["ref-1"].Amount);
    }

    [Fact]
    public async Task VerifyAsync_WhenAttemptNotBegun_ReportsUnknown()
    {
        // Arrange: an attempt with no persisted provider reference has not been begun, so there is nothing
        // to confirm and it must not fabricate a settlement.
        var provider = CreateProvider(isProduction: false);
        var attempt = new PaymentAttempt { Id = "attempt-1", Currency = "USD" };

        // Act
        var result = await provider.VerifyAsync(
            new VerifyPaymentContext { Attempt = attempt },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Unknown, result.Status);
        Assert.Null(result.TransactionId);
    }

    private static PayLaterCheckoutPaymentProvider CreateProvider(bool isProduction)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(isProduction ? Environments.Production : Environments.Development);

        return new PayLaterCheckoutPaymentProvider(
            hostEnvironment.Object,
            Mock.Of<IStringLocalizer<PayLaterCheckoutPaymentProvider>>());
    }
}
