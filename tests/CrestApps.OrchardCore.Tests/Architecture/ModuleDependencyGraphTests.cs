using System.Reflection;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Enforces the one-way dependency graph the reusable ecommerce foundation depends on. The compiler only
/// keeps a reference in assembly metadata when it is actually used, so an assembly appearing in the
/// forbidden set means real coupling was introduced and the direction was inverted.
/// </summary>
public sealed class ModuleDependencyGraphTests
{
    [Theory]
    [InlineData("CrestApps.OrchardCore.Checkout")]
    [InlineData("CrestApps.OrchardCore.Stripe")]
    [InlineData("CrestApps.OrchardCore.Taxation")]
    [InlineData("CrestApps.OrchardCore.Addresses")]
    public void PaymentsAbstractions_StaysProviderNeutral(string forbiddenPrefix)
    {
        // The payment event seam is the lowest layer: it must not know about checkout, taxation, addresses,
        // or any concrete gateway, so any provider can raise events without a dependency cycle.
        AssertNoReferenceTo(typeof(IPaymentEvent).Assembly, forbiddenPrefix);
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.Stripe")]
    [InlineData("CrestApps.OrchardCore.PayLater")]
    [InlineData("CrestApps.OrchardCore.Subscriptions")]
    public void CheckoutAbstractions_DoesNotDependOnConcreteProviders(string forbiddenPrefix)
    {
        // Checkout defines the provider contract; concrete providers depend on checkout, never the reverse.
        AssertNoReferenceTo(typeof(CheckoutReferenceTypes).Assembly, forbiddenPrefix);
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.Checkout")]
    [InlineData("CrestApps.OrchardCore.Payments")]
    [InlineData("CrestApps.OrchardCore.Stripe")]
    public void TaxationAbstractions_StaysIndependentOfPurchasing(string forbiddenPrefix)
    {
        // Tax determination is reusable outside a checkout, so it must not reference checkout or payments.
        AssertNoReferenceTo(typeof(TaxTable).Assembly, forbiddenPrefix);
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.Checkout")]
    [InlineData("CrestApps.OrchardCore.Taxation")]
    [InlineData("CrestApps.OrchardCore.Payments")]
    public void AddressesAbstractions_StaysAFoundationValueContract(string forbiddenPrefix)
    {
        // The address value snapshot is a leaf contract consumed by taxation, checkout, and shipping; it
        // must not depend on any of them.
        AssertNoReferenceTo(typeof(Address).Assembly, forbiddenPrefix);
    }

    private static void AssertNoReferenceTo(Assembly assembly, string forbiddenPrefix)
    {
        var offending = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null &&
                (string.Equals(name, forbiddenPrefix, StringComparison.Ordinal) ||
                 name.StartsWith(forbiddenPrefix + ".", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"'{assembly.GetName().Name}' must not reference '{forbiddenPrefix}' assemblies, but references: {string.Join(", ", offending)}.");
    }
}
