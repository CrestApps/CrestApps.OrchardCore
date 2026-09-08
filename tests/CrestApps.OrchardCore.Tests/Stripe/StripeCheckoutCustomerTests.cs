using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Stripe.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A checkout that sells a plan with a setup fee has two obligations, and the browser tokenizes one card for
/// both of them.
/// </summary>
/// <remarks>
/// Stripe attaches that payment method to a customer when the agreement is created, and then refuses to
/// confirm a payment intent that names a different customer — or none. Paying a setup fee by card therefore
/// failed outright with "the payment_method supplied belongs to the Customer …", which no fake could show:
/// it only appears once a real Stripe customer owns the card. These tests pin the two halves of the fix —
/// the one-time intent is created against a customer, and both obligations resolve the same one.
/// </remarks>
public sealed class StripeCheckoutCustomerTests
{
    /// <summary>
    /// The setup fee is charged to the customer the plan's card belongs to.
    /// </summary>
    [Fact]
    public async Task BeginAsync_WithAPreparedPaymentMethod_CreatesTheIntentAgainstTheCustomer()
    {
        CreateCheckoutPaymentIntentRequest captured = null;

        var intentService = new Mock<IStripePaymentIntentService>();
        intentService
            .Setup(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()))
            .ReturnsAsync((CreateCheckoutPaymentIntentRequest request) =>
            {
                captured = request;

                return new CreatePaymentIntentResponse { Id = "pi_1", ClientSecret = "pi_1_secret" };
            });

        var provider = CreateProvider(intentService.Object, out _);

        var result = await provider.BeginAsync(new BeginPaymentContext
        {
            Session = new CheckoutSession { SessionId = "session-1" },
            Attempt = CreateAttempt(),
            Invoice = new CheckoutInvoice { Currency = "USD" },
            ProviderData = new Dictionary<string, string>
            {
                [StripeRecurringPaymentProvider.PaymentMethodDataKey] = "pm_1",
            },
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal("cus_created", captured.CustomerId);
    }

    /// <summary>
    /// A checkout with nothing recurring in it tokenizes no card, so it stays customer-less exactly as
    /// before: creating a customer for a one-off card payment would be noise in the merchant's account.
    /// </summary>
    [Fact]
    public async Task BeginAsync_WithoutAPreparedPaymentMethod_CreatesNoCustomer()
    {
        CreateCheckoutPaymentIntentRequest captured = null;

        var intentService = new Mock<IStripePaymentIntentService>();
        intentService
            .Setup(service => service.CreateForCheckoutAsync(It.IsAny<CreateCheckoutPaymentIntentRequest>()))
            .ReturnsAsync((CreateCheckoutPaymentIntentRequest request) =>
            {
                captured = request;

                return new CreatePaymentIntentResponse { Id = "pi_1", ClientSecret = "pi_1_secret" };
            });

        var provider = CreateProvider(intentService.Object, out var customerService);

        await provider.BeginAsync(new BeginPaymentContext
        {
            Session = new CheckoutSession { SessionId = "session-1" },
            Attempt = CreateAttempt(),
            Invoice = new CheckoutInvoice { Currency = "USD" },
        }, TestContext.Current.CancellationToken);

        Assert.Null(captured.CustomerId);
        customerService.Verify(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()), Times.Never);
    }

    /// <summary>
    /// The two obligations of one checkout are two attempts. Resolving a customer per attempt would create a
    /// second one that the prepared card does not belong to, which is the same mismatch by another route.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ForTwoObligationsOfOneCheckout_ReturnsOneCustomer()
    {
        var customerService = CreateCustomerService();
        var resolver = new StripeCheckoutCustomerResolver(customerService.Object);

        var first = await resolver.ResolveAsync(new CheckoutSession { SessionId = "session-1" }, "session-1", "pm_1");
        var second = await resolver.ResolveAsync(new CheckoutSession { SessionId = "session-1" }, "session-1", "pm_1");

        Assert.Equal(first, second);
        customerService.Verify(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()), Times.Once);
    }

    /// <summary>
    /// A retry runs in a new scope, so the in-scope cache is empty. The key Stripe is asked to deduplicate on
    /// must therefore come from the checkout, not from the attempt that happens to be retrying.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_KeysTheCustomerByTheCheckout()
    {
        var customerService = CreateCustomerService();
        var resolver = new StripeCheckoutCustomerResolver(customerService.Object);

        CreateCustomerRequest captured = null;
        customerService
            .Setup(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync((CreateCustomerRequest request) =>
            {
                captured = request;

                return new CreateCustomerResponse { CustomerId = "cus_created" };
            });

        await resolver.ResolveAsync(new CheckoutSession { SessionId = "session-1" }, "session-1", "pm_1");

        Assert.Equal("checkout_session-1_customer", captured.IdempotencyKey);
    }

    /// <summary>
    /// A customer the client named is used as-is; nothing new is created for it.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_WithASuppliedCustomer_UsesIt()
    {
        var customerService = CreateCustomerService();
        var resolver = new StripeCheckoutCustomerResolver(customerService.Object);

        var resolved = await resolver.ResolveAsync(new CheckoutSession { SessionId = "session-1" }, "session-1", "pm_1", "cus_supplied");

        Assert.Equal("cus_supplied", resolved);
        customerService.Verify(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()), Times.Never);
    }

    private static PaymentAttempt CreateAttempt()
        => new()
        {
            ItemId = "attempt-1",
            SessionId = "session-1",
            ObligationId = "onetime",
            Currency = "USD",
            ExpectedAmount = 50m,
            IdempotencyKey = "attempt-1-key",
        };

    private static Mock<IStripeCustomerService> CreateCustomerService()
    {
        var customerService = new Mock<IStripeCustomerService>();
        customerService
            .Setup(service => service.CreateAsync(It.IsAny<CreateCustomerRequest>()))
            .ReturnsAsync(new CreateCustomerResponse { CustomerId = "cus_created" });

        return customerService;
    }

    private static StripeCheckoutPaymentProvider CreateProvider(
        IStripePaymentIntentService intentService,
        out Mock<IStripeCustomerService> customerService)
    {
        customerService = CreateCustomerService();

        return new StripeCheckoutPaymentProvider(
            intentService,
            Mock.Of<IStripeSubscriptionService>(),
            Mock.Of<IStripeRefundService>(),
            new StripeCheckoutCustomerResolver(customerService.Object),
            new InMemoryPaymentRefundStore(),
            new TestClock(DateTime.UtcNow),
            NullLogger<StripeCheckoutPaymentProvider>.Instance,
            new PassThroughStringLocalizer<StripeCheckoutPaymentProvider>());
    }
}
