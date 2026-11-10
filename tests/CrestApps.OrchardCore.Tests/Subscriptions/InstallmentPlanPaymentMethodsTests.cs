using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public sealed class InstallmentPlanPaymentMethodsTests
{
    [Fact]
    public void BuildProviderData_DropsEveryKeyOnlyServerCodeMaySet()
    {
        // Arrange
        var posted = new Dictionary<string, string>
        {
            ["paymentMethodId"] = "pm_card",
            [CheckoutPaymentDataKeys.OffSession] = "true",
            [CheckoutPaymentDataKeys.SavedPaymentMethodReference] = "pm_someone_else",
            [CheckoutPaymentDataKeys.DelegatedCredential] = "spt_test",
            [CheckoutPaymentDataKeys.DelegatedCredentialType] = "stripe_spt",
            [CheckoutPaymentDataKeys.ThreeDSecureOutcome] = "authenticated",
            [CheckoutPaymentDataKeys.ThreeDSecureCryptogram] = "AAAAAAAAAAAAAAAAAAAAAAAAAAA=",
        };

        // Act
        var sanitized = InstallmentPlanPaymentMethods.BuildProviderData(posted, savePaymentMethod: false);

        // Assert
        Assert.Equal("pm_card", Assert.Single(sanitized).Value);
    }

    [Fact]
    public void BuildProviderData_SavingTheCard_IsDecidedByTheServer()
    {
        // Act
        var sanitized = InstallmentPlanPaymentMethods.BuildProviderData(
            new Dictionary<string, string> { [CheckoutPaymentDataKeys.SavePaymentMethod] = "false" },
            savePaymentMethod: true);

        // Assert
        Assert.Equal("true", sanitized[CheckoutPaymentDataKeys.SavePaymentMethod]);
    }
}
