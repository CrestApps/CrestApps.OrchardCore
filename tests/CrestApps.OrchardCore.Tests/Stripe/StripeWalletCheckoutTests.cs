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
/// A signed-in buyer pays with one of their own saved cards, or keeps the card they typed; a saved card that isn't
/// theirs, or one named by a guest, is never charged.
/// </summary>
public sealed class StripeWalletCheckoutTests
{
    private readonly Mock<IStripePaymentIntentService> _intents = new();
    private readonly Mock<IStripeWalletService> _wallet = new();
    private CreateCheckoutPaymentIntentRequest _created;

    public StripeWalletCheckoutTests()
    {
        _intents
            .Setup(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()))
            .ReturnsAsync((CreateCheckoutPaymentIntentRequest request) =>
            {
                _created = request;

                return new CreatePaymentIntentResponse { Id = "pi_1", ClientSecret = "secret" };
            });

        _wallet.Setup(wallet => wallet.GetCustomerIdAsync("user-1")).ReturnsAsync("cus_wallet");
        _wallet.Setup(wallet => wallet.EnsureCustomerAsync("user-1", It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("cus_wallet");
        _wallet
            .Setup(wallet => wallet.FindCardAsync("user-1", "pm_mine", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeSavedCard { Id = "pm_mine", Brand = "visa", Last4 = "4242" });
    }

    [Fact]
    public async Task ASavedCardOfTheBuyerIsChargedOnTheirCustomer()
    {
        var result = await Provider().BeginAsync(Context("user-1", new() { [StripeCheckoutPaymentProvider.SavedCardDataKey] = "pm_mine" }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.RequiresAction);
        Assert.Equal("cus_wallet", _created.CustomerId);
        Assert.Equal("pm_mine", _created.PaymentMethodId);
        Assert.False(_created.SaveForOnSessionUse);
        Assert.False(_created.SaveForOffSessionUse);
    }

    [Fact]
    public async Task ASavedCardThatIsntTheBuyersIsRefused()
    {
        var result = await Provider().BeginAsync(Context("user-1", new() { [StripeCheckoutPaymentProvider.SavedCardDataKey] = "pm_someone_else" }), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        _intents.Verify(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task AGuestCantPayWithASavedCard()
    {
        var result = await Provider().BeginAsync(Context(null, new() { [StripeCheckoutPaymentProvider.SavedCardDataKey] = "pm_mine" }), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        _intents.Verify(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()), Times.Never);
    }

    [Fact]
    public async Task KeepingTheCardTypedInSavesItForTheBuyersOwnPurchases()
    {
        var result = await Provider().BeginAsync(Context("user-1", new()
        {
            [StripeRecurringPaymentProvider.PaymentMethodDataKey] = "pm_new",
            [StripeCheckoutPaymentProvider.RememberCardDataKey] = "true",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("cus_wallet", _created.CustomerId);
        Assert.Equal("pm_new", _created.PaymentMethodId);
        Assert.True(_created.SaveForOnSessionUse);
        Assert.False(_created.SaveForOffSessionUse);
    }

    [Fact]
    public async Task AGuestAskingToKeepTheirCardPaysWithoutKeepingIt()
    {
        var result = await Provider().BeginAsync(Context(null, new()
        {
            [StripeRecurringPaymentProvider.PaymentMethodDataKey] = "pm_new",
            [StripeCheckoutPaymentProvider.RememberCardDataKey] = "true",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.False(_created.SaveForOnSessionUse);
        _wallet.Verify(wallet => wallet.EnsureCustomerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private StripeCheckoutPaymentProvider Provider()
    {
        var customers = new Mock<IStripeCustomerService>();
        customers
            .Setup(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync(new CreateCustomerResponse { CustomerId = "cus_checkout" });

        return new StripeCheckoutPaymentProvider(
            _intents.Object,
            Mock.Of<IStripePaymentMethodService>(),
            Mock.Of<IStripeSubscriptionService>(),
            Mock.Of<IStripeRefundService>(),
            new StripeCheckoutCustomerResolver(customers.Object),
            _wallet.Object,
            new InMemoryPaymentRefundStore(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());
    }

    private static BeginPaymentContext Context(string ownerId, Dictionary<string, string> providerData)
        => new()
        {
            Session = new CheckoutSession { SessionId = "session-1", OwnerId = ownerId },
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
}
