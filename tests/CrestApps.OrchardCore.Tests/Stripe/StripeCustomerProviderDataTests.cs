using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Services;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// SEC-C17b: the Stripe customer a payment is charged to is resolved on the server. A browser or an app can't name one
/// in the provider data (a leaked <c>cus_</c> id would otherwise charge, or save cards to, someone else's customer).
/// </summary>
public sealed class StripeCustomerProviderDataTests
{
    [Fact]
    public void TheStripeCustomerKey_IsServerOnly()
        => Assert.True(CheckoutPaymentDataKeys.IsServerOnly(StripeRecurringPaymentProvider.CustomerDataKey));

    [Fact]
    public void SanitizeProviderData_DropsACustomerTheBrowserNamed()
    {
        // Arrange
        var posted = new Dictionary<string, string>
        {
            [StripeRecurringPaymentProvider.CustomerDataKey] = "cus_someone_else",
            ["paymentMethodId"] = "pm_1",
        };

        // Act
        var sanitized = global::CrestApps.OrchardCore.Checkout.Endpoints.PaymentEndpoints.SanitizeProviderData(posted);

        // Assert
        Assert.False(sanitized.ContainsKey(StripeRecurringPaymentProvider.CustomerDataKey));
        Assert.Equal("pm_1", sanitized["paymentMethodId"]);
    }
}
