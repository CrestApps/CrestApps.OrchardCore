using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A 3-D Secure result an agent got is imported into the payment with Stripe's
/// <c>payment_method_options[card][three_d_secure]</c>: it confirms the PaymentIntent the same attempt began, never a
/// second one, and a result that doesn't let the payment go ahead never reaches Stripe.
/// </summary>
public sealed class StripeDelegatedAuthenticationTests
{
    private readonly Mock<IStripePaymentIntentService> _intents = new();
    private ChargeDelegatedCredentialRequest _charged;
    private ConfirmDelegatedCredentialRequest _confirmed;

    public StripeDelegatedAuthenticationTests()
    {
        _intents
            .Setup(service => service.ChargeDelegatedAsync(It.IsAny<ChargeDelegatedCredentialRequest>()))
            .ReturnsAsync((ChargeDelegatedCredentialRequest request) =>
            {
                _charged = request;

                return new ChargeDelegatedCredentialResponse { Succeeded = true, PaymentIntentId = "pi_new" };
            });

        _intents
            .Setup(service => service.ConfirmDelegatedAsync(It.IsAny<ConfirmDelegatedCredentialRequest>()))
            .ReturnsAsync((ConfirmDelegatedCredentialRequest request) =>
            {
                _confirmed = request;

                return new ChargeDelegatedCredentialResponse { Succeeded = true, PaymentIntentId = request.PaymentIntentId };
            });
    }

    [Fact]
    public void GetThreeDSecureFields_SendsStripesImportParametersAndRefusesASecondChallenge()
    {
        // Arrange
        var import = new StripeThreeDSecureImport
        {
            Cryptogram = "M6+990I6FLD8Y6rZz9d5QbfrMNY=",
            ElectronicCommerceIndicator = "02",
            TransactionId = "5f5d08f2-8c36-4f72-99d1-57b4fb70b7d5",
            Version = "2.1.0",
            AresTransStatus = "Y",
        };

        // Act
        var fields = StripePaymentIntentService.GetThreeDSecureFields(import);

        // Assert
        Assert.Equal(
            [
                ("payment_method_options[card][three_d_secure][cryptogram]", "M6+990I6FLD8Y6rZz9d5QbfrMNY="),
                ("payment_method_options[card][three_d_secure][electronic_commerce_indicator]", "02"),
                ("payment_method_options[card][three_d_secure][transaction_id]", "5f5d08f2-8c36-4f72-99d1-57b4fb70b7d5"),
                ("payment_method_options[card][three_d_secure][version]", "2.1.0"),
                ("payment_method_options[card][three_d_secure][ares_trans_status]", "Y"),
                ("error_on_requires_action", "true"),
            ],
            fields.Select(field => (field.Key, field.Value)));
    }

    [Fact]
    public void GetThreeDSecureFields_WithoutAResult_SendsNothing()
    {
        // Act & Assert
        Assert.Empty(StripePaymentIntentService.GetThreeDSecureFields(null));
        Assert.DoesNotContain(
            StripePaymentIntentService.GetThreeDSecureFields(new StripeThreeDSecureImport { Cryptogram = "c", ElectronicCommerceIndicator = "05", TransactionId = "t", Version = "2.2.0" }),
            field => field.Key.EndsWith("[ares_trans_status]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BeginAsync_WithoutAResult_ChargesWithoutImporting()
    {
        // Act
        var result = await Provider().BeginAsync(Context(providerReference: null, authentication: null), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(_charged.ThreeDSecure);
        Assert.Equal("checkout_session-1_onetime", _charged.IdempotencyKey);
        _intents.Verify(service => service.ConfirmDelegatedAsync(It.IsAny<ConfirmDelegatedCredentialRequest>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_WithAResultForAPaymentTheAttemptBegan_ConfirmsThatPaymentIntent()
    {
        // Arrange
        var authentication = ThreeDSecureResultTests.Authenticated();

        // Act
        var result = await Provider().BeginAsync(Context("pi_earlier", authentication), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("pi_earlier", result.ProviderReference);
        Assert.Equal("pi_earlier", _confirmed.PaymentIntentId);
        Assert.Equal(DelegatedCredentialTypes.StripeSharedPaymentToken, _confirmed.CredentialType);
        Assert.Equal(authentication.Cryptogram, _confirmed.ThreeDSecure.Cryptogram);
        Assert.Equal(authentication.ElectronicCommerceIndicator, _confirmed.ThreeDSecure.ElectronicCommerceIndicator);
        Assert.Equal(authentication.TransactionId, _confirmed.ThreeDSecure.TransactionId);
        Assert.Equal(authentication.Version, _confirmed.ThreeDSecure.Version);
        Assert.StartsWith("checkout_session-1_onetime_3ds_", _confirmed.IdempotencyKey, StringComparison.Ordinal);
        _intents.Verify(service => service.ChargeDelegatedAsync(It.IsAny<ChargeDelegatedCredentialRequest>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_WithAResultForANewAttempt_ImportsItIntoTheNewPaymentIntent()
    {
        // Act
        var result = await Provider().BeginAsync(Context(providerReference: null, ThreeDSecureResultTests.Authenticated()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("05", _charged.ThreeDSecure.ElectronicCommerceIndicator);
        Assert.Equal("checkout_session-1_onetime", _charged.IdempotencyKey);
        _intents.Verify(service => service.ConfirmDelegatedAsync(It.IsAny<ConfirmDelegatedCredentialRequest>()), Times.Never);
    }

    [Fact]
    public void GetAuthenticationIdempotencyKey_IsStableForTheSameResultAndDiffersForAnother()
    {
        // Arrange
        var attempt = new PaymentAttempt { IdempotencyKey = "checkout_s_one-time_1000_USD" };
        var first = ThreeDSecureResultTests.Authenticated();
        var other = ThreeDSecureResultTests.Authenticated();

        other.TransactionId = "aaa65c7b-b0fc-4e71-bd6c-29c87acad489";

        // Act & Assert
        Assert.Equal(
            StripeCheckoutPaymentProvider.GetAuthenticationIdempotencyKey(attempt, first),
            StripeCheckoutPaymentProvider.GetAuthenticationIdempotencyKey(attempt, ThreeDSecureResultTests.Authenticated()));
        Assert.NotEqual(
            StripeCheckoutPaymentProvider.GetAuthenticationIdempotencyKey(attempt, first),
            StripeCheckoutPaymentProvider.GetAuthenticationIdempotencyKey(attempt, other));
    }

    [Fact]
    public async Task BeginAsync_WithADeniedResult_DeclinesWithoutChargingAndCancelsTheEarlierPaymentIntent()
    {
        // Arrange
        _intents
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentDetails { Id = "pi_earlier", Status = "requires_action" });

        // Act
        var result = await Provider().BeginAsync(Context("pi_earlier", new ThreeDSecureResult { Outcome = ThreeDSecureOutcomes.Denied }), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        _intents.Verify(service => service.ChargeDelegatedAsync(It.IsAny<ChargeDelegatedCredentialRequest>()), Times.Never);
        _intents.Verify(service => service.ConfirmDelegatedAsync(It.IsAny<ConfirmDelegatedCredentialRequest>()), Times.Never);
        _intents.Verify(service => service.CancelAsync(It.Is<CancelPaymentIntentRequest>(request => request.PaymentIntentId == "pi_earlier")), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_WithAnIncompleteResult_FailsWithoutCallingStripe()
    {
        // Arrange
        var authentication = ThreeDSecureResultTests.Authenticated();

        authentication.ElectronicCommerceIndicator = "09";

        // Act
        var result = await Provider().BeginAsync(Context("pi_earlier", authentication), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
        _intents.Verify(service => service.ConfirmDelegatedAsync(It.IsAny<ConfirmDelegatedCredentialRequest>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_WhenTheBankStillWantsToAuthenticate_ReportsItWithTheCardBrand()
    {
        // Arrange
        _intents
            .Setup(service => service.ChargeDelegatedAsync(It.IsAny<ChargeDelegatedCredentialRequest>()))
            .ReturnsAsync(new ChargeDelegatedCredentialResponse { RequiresAuthentication = true, Declined = true, PaymentIntentId = "pi_new", CardBrand = "mastercard", ErrorMessage = "Authentication required." });

        // Act
        var result = await Provider().BeginAsync(Context(providerReference: null, authentication: null), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.RequiresAuthentication);
        Assert.Equal("mastercard", result.CardBrand);
        Assert.Equal("pi_new", result.ProviderReference);
    }

    [Fact]
    public async Task BeginAsync_WhenTheBankAsksTheBuyer_ReportsTheChallengeAndTheCardBrand()
    {
        // Arrange
        _intents
            .Setup(service => service.ChargeDelegatedAsync(It.IsAny<ChargeDelegatedCredentialRequest>()))
            .ReturnsAsync(new ChargeDelegatedCredentialResponse { RequiresAction = true, PaymentIntentId = "pi_new", RedirectUrl = "https://hooks.stripe.com/3d_secure", CardBrand = "visa" });

        // Act
        var result = await Provider().BeginAsync(Context(providerReference: null, authentication: null), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresAction);
        Assert.Equal("visa", result.CardBrand);
        Assert.True(Provider().Capabilities.AcceptsExternalAuthentication);
    }

    private StripeCheckoutPaymentProvider Provider()
        => new(
            _intents.Object,
            Mock.Of<IStripePaymentMethodService>(),
            Mock.Of<IStripeSubscriptionService>(),
            Mock.Of<IStripeRefundService>(),
            new StripeCheckoutCustomerResolver(Mock.Of<IStripeCustomerService>()),
            Mock.Of<IStripeWalletService>(),
            new InMemoryPaymentRefundStore(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());

    private static BeginPaymentContext Context(string providerReference, ThreeDSecureResult authentication)
    {
        var providerData = new Dictionary<string, string>
        {
            [CheckoutPaymentDataKeys.DelegatedCredential] = "spt_123",
            [CheckoutPaymentDataKeys.DelegatedCredentialType] = DelegatedCredentialTypes.StripeSharedPaymentToken,
        };

        authentication?.CopyTo(providerData);

        return new BeginPaymentContext
        {
            Session = new CheckoutSession { SessionId = "session-1" },
            Attempt = new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ProviderKey = StripeConstants.ProcessorKey,
                ObligationId = CheckoutObligations.OneTime,
                ExpectedAmount = 30m,
                Currency = "USD",
                State = providerReference is null ? PaymentAttemptState.Created : PaymentAttemptState.Pending,
                ProviderReference = providerReference,
                IdempotencyKey = "checkout_session-1_" + CheckoutObligations.OneTime,
            },
            ProviderData = providerData,
        };
    }
}
