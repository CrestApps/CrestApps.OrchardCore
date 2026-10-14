using System.Reflection;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Subscriptions.ViewModels;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Guards the view models that Orchard Core builds shapes from.
/// </summary>
/// <remarks>
/// <para>
/// Orchard Core creates a strongly typed shape by generating a Castle proxy that derives from the view
/// model. A sealed view model cannot be derived from, so the shape factory throws <c>TypeLoadException</c>,
/// the display manager swallows it, and the editor renders as an <em>empty box</em> with no error anywhere
/// the user can see.
/// </para>
/// <para>
/// That failure mode is why this test exists. It is invisible to every other kind of test: the driver runs,
/// the view compiles, the page returns 200, and the panel is simply missing. Five checkout and subscription
/// editors — including the payment method list, which is the one thing a customer must have to pay — shipped
/// blank this way and were only caught by opening the page.
/// </para>
/// </remarks>
public sealed class ShapeViewModelContractTests
{
    /// <summary>
    /// The view models passed to <c>Initialize&lt;T&gt;</c> by the commerce display drivers.
    /// </summary>
    public static TheoryData<Type> ShapeViewModels =>
    [
        typeof(CheckoutPaymentMethodsViewModel),
        typeof(CouponViewModel),
        typeof(SubscriptionEntitlementPartViewModel),
        typeof(TenantProvisioningStepViewModel),
        typeof(SubscriptionPartViewModel),
        typeof(SubscriptionSettingsViewModel),
        typeof(UserRegistrationStepViewModel),
        typeof(CrestApps.OrchardCore.Subscriptions.Models.CreateUserViewModel),
    ];

    /// <summary>
    /// A sealed view model renders an empty editor instead of failing loudly, so sealing one is never safe.
    /// </summary>
    /// <param name="type">The view model type.</param>
    [Theory]
    [MemberData(nameof(ShapeViewModels))]
    public void ShapeViewModels_AreNotSealed(Type type)
    {
        Assert.False(
            type.IsSealed,
            $"'{type.Name}' is used to build a shape, so Orchard Core must be able to derive a proxy from it. Sealing it makes the editor render empty with no visible error.");
    }

    /// <summary>
    /// The proxy is built by subclassing, which needs a constructor that takes no arguments.
    /// </summary>
    /// <param name="type">The view model type.</param>
    [Theory]
    [MemberData(nameof(ShapeViewModels))]
    public void ShapeViewModels_HaveAParameterlessConstructor(Type type)
    {
        Assert.True(
            type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null,
            $"'{type.Name}' must expose a public parameterless constructor so the shape factory can create it.");
    }
}
