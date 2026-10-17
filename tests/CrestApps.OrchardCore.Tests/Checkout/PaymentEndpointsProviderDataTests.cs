using CrestApps.OrchardCore.Checkout.Endpoints;
using CrestApps.OrchardCore.Checkout.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// What the customer's browser posts to begin a payment never decides whether a card is kept, and can never spend a
/// card that was kept earlier. Only the server sets those keys.
/// </summary>
public sealed class PaymentEndpointsProviderDataTests
{
    [Fact]
    public void SanitizeProviderData_DropsTheKeysOnlyTheServerMaySet()
    {
        // Arrange
        var posted = new Dictionary<string, string>
        {
            ["paymentMethodId"] = "pm_browser",
            [CheckoutPaymentDataKeys.SavePaymentMethod] = "true",
            [CheckoutPaymentDataKeys.OffSession] = "true",
            [CheckoutPaymentDataKeys.SavedCustomerReference] = "cus_someone_else",
            [CheckoutPaymentDataKeys.SavedPaymentMethodReference] = "pm_someone_else",
        };

        // Act
        var sanitized = PaymentEndpoints.SanitizeProviderData(posted);

        // Assert
        var only = Assert.Single(sanitized);

        Assert.Equal("paymentMethodId", only.Key);
        Assert.Equal("pm_browser", only.Value);
    }

    [Fact]
    public void SanitizeProviderData_WithOnlyServerKeys_LeavesNothing()
    {
        // Arrange
        var posted = new Dictionary<string, string>
        {
            [CheckoutPaymentDataKeys.OffSession] = "true",
        };

        // Act
        var sanitized = PaymentEndpoints.SanitizeProviderData(posted);

        // Assert
        Assert.Null(sanitized);
    }
}
