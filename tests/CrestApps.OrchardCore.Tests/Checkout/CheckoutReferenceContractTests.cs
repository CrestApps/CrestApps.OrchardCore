using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutReferenceContractTests
{
    [Fact]
    public void OrderReferenceType_HasStableValue()
    {
        // The order reference type is persisted on checkout sessions and correlated by payment attempts and
        // refunds, so its value must never drift.
        Assert.Equal("Order", CheckoutReferenceTypes.Order);
    }

    [Fact]
    public void CheckoutSessionStore_ExposesReverseLookupByReference()
    {
        // Arrange
        var method = typeof(ICheckoutSessionStore).GetMethod(
            nameof(ICheckoutSessionStore.GetByReferenceAsync),
            BindingFlags.Public | BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);

        var parameters = method.GetParameters();

        Assert.Equal("referenceType", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("referenceId", parameters[1].Name);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal("referenceVersionId", parameters[2].Name);
        Assert.True(parameters[2].IsOptional);
    }
}
