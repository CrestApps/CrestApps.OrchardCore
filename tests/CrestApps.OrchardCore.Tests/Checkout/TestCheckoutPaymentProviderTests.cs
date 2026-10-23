using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The test payment provider stands in for a gateway in development and automated tests, so it has to honor the
/// same contracts a gateway does: a reference from begin, an authoritative amount from verify, a refund reference,
/// and a payment method that can be kept and charged again. The magic amounts are what let a test reach the
/// declined and still-pending paths without a gateway.
/// </summary>
public sealed class TestCheckoutPaymentProviderTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BeginAsync_SucceedsWithNoClientAction()
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt(30m);

        // Act
        var result = await provider.BeginAsync(new BeginPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresAction);
        Assert.Null(result.ClientSecret);
        Assert.Null(result.RedirectUrl);
        Assert.StartsWith("test_", result.ProviderReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeginAsync_ForAResumedAttempt_KeepsItsReference()
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt(30m);
        attempt.ProviderReference = "test_pay_existing";

        // Act
        var result = await provider.BeginAsync(new BeginPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("test_pay_existing", result.ProviderReference);
    }

    [Fact]
    public async Task VerifyAsync_ReportsTheAttemptAmountAsAnAuthoritativeTestCharge()
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt(100m, taxAmount: 8m);
        attempt.ProviderReference = "test_pay_1";

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.True(result.ReportsAuthoritativeAmount);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(8m, result.TaxAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(GatewayMode.Testing, result.GatewayMode);
        Assert.Equal("test_pay_1", result.TransactionId);
    }

    [Fact]
    public async Task VerifyAsync_ForAnAttemptThatWasNeverBegun_ReportsUnknown()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = CreateAttempt(30m) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Unknown, result.Status);
    }

    [Theory]
    [InlineData(10.02, 0, "USD")]
    [InlineData(10.00, 0.02, "USD")]
    [InlineData(1002, 0, "JPY")]
    public async Task BeginAsync_WhenTheAmountEndsIn02_Declines(double amount, double taxAmount, string currency)
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt((decimal)amount, (decimal)taxAmount, currency);

        // Act
        var result = await provider.BeginAsync(new BeginPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task VerifyAsync_WhenTheAmountEndsIn05_StaysPendingUntilTheSweepWouldPickItUp()
    {
        // Arrange
        var clock = new TestClock(_now);
        var provider = CreateProvider(clock);
        var attempt = CreateAttempt(10.05m);
        attempt.ProviderReference = "test_pay_1";
        attempt.CreatedUtc = _now;

        // Act
        var whileFresh = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        clock.UtcNow = _now + TestCheckoutPaymentProvider.PendingSettlementDelay;

        var onceStale = await provider.VerifyAsync(new VerifyPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Unknown, whileFresh.Status);
        Assert.Equal(PaymentStatus.Succeeded, onceStale.Status);
        Assert.Equal(10.05m, onceStale.Amount);
    }

    [Fact]
    public async Task RefundAsync_SucceedsForTheRequestedAmount()
    {
        // Arrange
        var provider = CreateProvider();
        var context = new RefundPaymentContext
        {
            OriginalTransactionId = "test_pay_1",
            Amount = 40m,
            Currency = "USD",
            IdempotencyKey = "refund_1",
        };

        // Act
        var first = await provider.RefundAsync(context, TestContext.Current.CancellationToken);
        var retried = await provider.RefundAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RefundStatus.Succeeded, first.Status);
        Assert.Equal(40m, first.Amount);
        Assert.Equal("USD", first.Currency);
        Assert.Equal(GatewayMode.Testing, first.GatewayMode);
        Assert.False(string.IsNullOrEmpty(first.ProviderRefundReference));

        // A retry with the same idempotency key is the same refund, not a second one.
        Assert.Equal(first.ProviderRefundReference, retried.ProviderRefundReference);
    }

    [Fact]
    public async Task GetSavedPaymentMethodAsync_AfterAPaymentAskedToKeepIt_ReturnsAMethodThatCanBeChargedOffSession()
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt(30m);
        var begun = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = attempt,
                ProviderData = new Dictionary<string, string> { [CheckoutPaymentDataKeys.SavePaymentMethod] = "true" },
            },
            TestContext.Current.CancellationToken);

        attempt.ProviderReference = begun.ProviderReference;
        attempt.State = PaymentAttemptState.Succeeded;

        // Act
        var saved = await provider.GetSavedPaymentMethodAsync(attempt, TestContext.Current.CancellationToken);

        var charge = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = CreateAttempt(25m),
                ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(saved),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(TestCheckoutPaymentProvider.ProviderKey, saved.ProviderKey);
        Assert.False(string.IsNullOrEmpty(saved.CustomerReference));
        Assert.False(string.IsNullOrEmpty(saved.PaymentMethodReference));
        Assert.False(saved.IsExpired(_now));
        Assert.True(charge.Succeeded);
        Assert.False(charge.RequiresAction);
    }

    [Fact]
    public async Task GetSavedPaymentMethodAsync_AfterAPaymentThatDidNotAskToKeepIt_ReturnsNull()
    {
        // Arrange
        var provider = CreateProvider();
        var attempt = CreateAttempt(30m);
        var begun = await provider.BeginAsync(new BeginPaymentContext { Attempt = attempt }, TestContext.Current.CancellationToken);
        attempt.ProviderReference = begun.ProviderReference;

        // Act
        var saved = await provider.GetSavedPaymentMethodAsync(attempt, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(saved);
    }

    [Fact]
    public async Task BeginAsync_AnOffSessionChargeOfAMethodItDidNotKeep_Fails()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = CreateAttempt(25m),
                ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(new SavedPaymentMethod
                {
                    CustomerReference = "cus_123",
                    PaymentMethodReference = "pm_123",
                }),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
    }

    [Fact]
    public async Task BeginAsync_AnOffSessionChargeEndingIn03_RequiresTheCardholdersApproval()
    {
        // Arrange
        var provider = CreateProvider();
        var saved = await KeepPaymentMethodAsync(provider);

        // Act
        var result = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = CreateAttempt(25.03m),
                ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(saved),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        Assert.True(result.RequiresAuthentication);
        Assert.False(string.IsNullOrEmpty(result.ProviderReference));
    }

    [Fact]
    public async Task BeginAsync_AnOffSessionChargeEndingIn02_IsDeclinedWithoutAskingForApproval()
    {
        // Arrange
        var provider = CreateProvider();
        var saved = await KeepPaymentMethodAsync(provider);

        // Act
        var result = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = CreateAttempt(25.02m),
                ProviderData = CheckoutPaymentDataKeys.ForOffSessionCharge(saved),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Declined);
        Assert.False(result.RequiresAuthentication);
    }

    [Fact]
    public async Task BeginAsync_APaymentEndingIn03MadeByThePayer_Succeeds()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = await provider.BeginAsync(new BeginPaymentContext { Attempt = CreateAttempt(25.03m) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresAuthentication);
    }

    [Fact]
    public async Task BeginRecurringAsync_EstablishesAnAgreementAndHonorsTheDeclineAmount()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var established = await provider.BeginRecurringAsync(
            new BeginRecurringPaymentContext { Attempt = CreateAttempt(10m, obligationId: "recurring:Month:1") },
            TestContext.Current.CancellationToken);

        var declined = await provider.BeginRecurringAsync(
            new BeginRecurringPaymentContext { Attempt = CreateAttempt(10.02m, obligationId: "recurring:Month:1") },
            TestContext.Current.CancellationToken);

        var canceled = await provider.CancelRecurringAsync(new CancelRecurringPaymentContext { ProviderSubscriptionId = established.ProviderReference }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(established.Succeeded);
        Assert.False(established.RequiresAction);
        Assert.True(declined.Declined);
        Assert.True(canceled.Succeeded);
    }

    [Fact]
    public void Capabilities_DescribeAGatewayThatCollectsMoneyNow()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var capabilities = provider.Capabilities;

        // Assert
        Assert.True(capabilities.SupportsOneTimePayments);
        Assert.True(capabilities.SupportsRecurringPayments);
        Assert.True(capabilities.SupportsCombinedOneTimeAndRecurring);
        Assert.True(capabilities.SupportsRefunds);
        Assert.True(capabilities.SupportsSavedPaymentMethods);
        Assert.False(capabilities.DefersPayment);
        Assert.Equal(TestCheckoutPaymentProvider.ProviderKey, provider.Key);
    }

    // Takes a payment that keeps its payment method and reads back what was kept, as a checkout that keeps the card does.
    private static async Task<SavedPaymentMethod> KeepPaymentMethodAsync(TestCheckoutPaymentProvider provider)
    {
        var attempt = CreateAttempt(30m);
        var begun = await provider.BeginAsync(
            new BeginPaymentContext
            {
                Attempt = attempt,
                ProviderData = new Dictionary<string, string> { [CheckoutPaymentDataKeys.SavePaymentMethod] = "true" },
            },
            TestContext.Current.CancellationToken);

        attempt.ProviderReference = begun.ProviderReference;
        attempt.State = PaymentAttemptState.Succeeded;

        return await provider.GetSavedPaymentMethodAsync(attempt, TestContext.Current.CancellationToken);
    }

    internal static TestCheckoutPaymentProvider CreateProvider(TestClock clock = null)
        => new(clock ?? new TestClock(_now), new PassThroughStringLocalizer<TestCheckoutPaymentProvider>());

    private static PaymentAttempt CreateAttempt(
        decimal amount,
        decimal taxAmount = 0m,
        string currency = "USD",
        string obligationId = CheckoutObligations.OneTime)
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ProviderKey = TestCheckoutPaymentProvider.ProviderKey,
            ObligationId = obligationId,
            ExpectedAmount = amount,
            ExpectedTaxAmount = taxAmount,
            Currency = currency,
            State = PaymentAttemptState.Created,
            IdempotencyKey = "checkout_session-1_" + obligationId,
        };
}
