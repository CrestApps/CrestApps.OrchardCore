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
using Xunit;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A payment plan keeps the card used for its down payment and charges it on each due date without the customer.
/// These pin the two Stripe halves of that: keeping the card while charging it, and charging the kept card.
/// </summary>
public sealed class StripeSavedPaymentMethodTests
{
    [Fact]
    public async Task BeginAsync_SavingTheCard_AuthenticatesItForLaterUseOnTheCustomer()
    {
        // Arrange
        CreateCheckoutPaymentIntentRequest captured = null;

        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()))
            .ReturnsAsync((CreateCheckoutPaymentIntentRequest request) =>
            {
                captured = request;

                return new CreatePaymentIntentResponse { Id = "pi_1", ClientSecret = "secret" };
            });

        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(new Dictionary<string, string>
        {
            [StripeRecurringPaymentProvider.PaymentMethodDataKey] = "pm_card",
            [CheckoutPaymentDataKeys.SavePaymentMethod] = "true",
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresAction);
        Assert.True(captured.SaveForOffSessionUse);
        Assert.Equal("pm_card", captured.PaymentMethodId);
        Assert.Equal("cus_created", captured.CustomerId);
    }

    [Fact]
    public async Task BeginAsync_SavingTheCardWithoutATokenizedCard_RefusesToTakeThePayment()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(new Dictionary<string, string>
        {
            [CheckoutPaymentDataKeys.SavePaymentMethod] = "true",
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
        intents.Verify(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_OffSession_ChargesTheSavedCardWithNothingLeftForABrowser()
    {
        // Arrange
        ChargeOffSessionRequest captured = null;

        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ReturnsAsync((ChargeOffSessionRequest request) =>
            {
                captured = request;

                return new ChargeOffSessionResponse { Succeeded = true, PaymentIntentId = "pi_auto", Status = "succeeded" };
            });

        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(CheckoutPaymentDataKeys.ForOffSessionCharge(new SavedPaymentMethod
        {
            CustomerReference = "cus_saved",
            PaymentMethodReference = "pm_saved",
        })), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresAction);
        Assert.Equal("pi_auto", result.ProviderReference);
        Assert.Equal("cus_saved", captured.CustomerId);
        Assert.Equal("pm_saved", captured.PaymentMethodId);
        Assert.Equal(50m, captured.Amount);
        Assert.Equal("attempt-1-key", captured.IdempotencyKey);
        intents.Verify(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task BeginAsync_OffSessionDecline_IsReportedAsADeclineWithStripesReason()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ReturnsAsync(new ChargeOffSessionResponse { Succeeded = false, PaymentIntentId = "pi_declined", ErrorMessage = "Your card was declined.", DeclineCode = "generic_decline" });

        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(OffSessionData()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.Declined);
        Assert.Equal("Your card was declined.", result.ErrorMessage);
        Assert.Equal("pi_declined", result.ProviderReference);
    }

    [Fact]
    public async Task BeginAsync_OffSessionNeedingAuthentication_TellsTheOperatorTheCustomerMustPay()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ReturnsAsync(new ChargeOffSessionResponse { Succeeded = false, RequiresAuthentication = true, ErrorMessage = "authentication_required" });

        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(OffSessionData()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Declined);
        Assert.Contains("customer needs to pay it themselves", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeginAsync_OffSessionTransportFailure_IsNotADecline()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(OffSessionData()), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(result.Declined);
    }

    [Fact]
    public async Task BeginAsync_OffSessionWithoutTheSavedReferences_ChargesNothing()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        var provider = CreateProvider(intents.Object);

        // Act
        var result = await provider.BeginAsync(CreateContext(new Dictionary<string, string>
        {
            [CheckoutPaymentDataKeys.OffSession] = "true",
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        intents.Verify(service => service.ChargeOffSessionAsync(It.IsAny<ChargeOffSessionRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetSavedPaymentMethodAsync_ForACardKeptForLaterUse_DescribesIt()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentDetails { Id = "pi_1", Status = "succeeded", CustomerId = "cus_1", PaymentMethodId = "pm_1", SetupFutureUsage = "off_session" });

        var methods = new Mock<IStripePaymentMethodService>();
        methods
            .Setup(service => service.GetInformationAsync("pm_1"))
            .ReturnsAsync(new StripePaymentMethodInfoResponse
            {
                Card = new StripePaymentCardInfoResponse { Brand = "visa", LastFour = "4242", ExpirationMonth = 12, ExpirationYear = 2034 },
            });

        var provider = CreateProvider(intents.Object, methods.Object);

        // Act
        var saved = await provider.GetSavedPaymentMethodAsync(new PaymentAttempt { ProviderReference = "pi_1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(StripeConstants.ProcessorKey, saved.ProviderKey);
        Assert.Equal("cus_1", saved.CustomerReference);
        Assert.Equal("pm_1", saved.PaymentMethodReference);
        Assert.Equal("Visa •••• 4242", saved.Describe());
        Assert.Equal(12, saved.ExpirationMonth);
        Assert.Equal(2034, saved.ExpirationYear);
    }

    [Fact]
    public async Task GetSavedPaymentMethodAsync_ForACardUsedOnce_ReturnsNothing()
    {
        // Arrange
        var intents = new Mock<IStripePaymentIntentService>();
        intents
            .Setup(service => service.RetrieveAsync(It.IsAny<RetrievePaymentIntentRequest>()))
            .ReturnsAsync(new PaymentIntentDetails { Id = "pi_1", Status = "succeeded", CustomerId = "cus_1", PaymentMethodId = "pm_1" });

        var provider = CreateProvider(intents.Object);

        // Act
        var saved = await provider.GetSavedPaymentMethodAsync(new PaymentAttempt { ProviderReference = "pi_1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(saved);
    }

    [Fact]
    public void Capabilities_DeclareSavedPaymentMethods()
        => Assert.True(CreateProvider(Mock.Of<IStripePaymentIntentService>()).Capabilities.SupportsSavedPaymentMethods);

    private static Dictionary<string, string> OffSessionData()
        => CheckoutPaymentDataKeys.ForOffSessionCharge(new SavedPaymentMethod
        {
            CustomerReference = "cus_saved",
            PaymentMethodReference = "pm_saved",
        });

    private static BeginPaymentContext CreateContext(IReadOnlyDictionary<string, string> providerData)
        => new()
        {
            Session = new CheckoutSession { SessionId = "session-1" },
            Attempt = new PaymentAttempt
            {
                ItemId = "attempt-1",
                SessionId = "session-1",
                ObligationId = "onetime",
                Currency = "USD",
                ExpectedAmount = 50m,
                IdempotencyKey = "attempt-1-key",
            },
            Invoice = new CheckoutInvoice { Currency = "USD" },
            ProviderData = providerData,
        };

    private static StripeCheckoutPaymentProvider CreateProvider(IStripePaymentIntentService intents, IStripePaymentMethodService methods = null)
    {
        var customers = new Mock<IStripeCustomerService>();
        customers
            .Setup(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync(new CreateCustomerResponse { CustomerId = "cus_created" });

        return new StripeCheckoutPaymentProvider(
            intents,
            methods ?? Mock.Of<IStripePaymentMethodService>(),
            Mock.Of<IStripeSubscriptionService>(),
            Mock.Of<IStripeRefundService>(),
            new StripeCheckoutCustomerResolver(customers.Object),
            new InMemoryPaymentRefundStore(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());
    }
}
