using System.Reflection;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductsModuleBoundaryTests
{
    [Theory]
    [InlineData("CrestApps.OrchardCore.Payments")]
    [InlineData("CrestApps.OrchardCore.Checkout")]
    [InlineData("CrestApps.OrchardCore.Stripe")]
    [InlineData("CrestApps.OrchardCore.PayLater")]
    [InlineData("CrestApps.OrchardCore.Subscriptions")]
    [InlineData("CrestApps.OrchardCore.Transactions")]
    public void ProductsCore_DoesNotDependOnPaymentOrCheckout(string forbiddenPrefix)
    {
        // The catalog domain must stay reusable independently of any purchasing pipeline. Payment and
        // checkout consume the resolved sellable snapshot, so the dependency direction is one-way and the
        // Products.Core assembly must never reference the payment or checkout assemblies.
        AssertNoReferenceTo(typeof(ProductPart).Assembly, forbiddenPrefix);
    }

    [Theory]
    [InlineData("CrestApps.OrchardCore.Payments")]
    [InlineData("CrestApps.OrchardCore.Checkout")]
    [InlineData("CrestApps.OrchardCore.Stripe")]
    [InlineData("CrestApps.OrchardCore.PayLater")]
    [InlineData("CrestApps.OrchardCore.Subscriptions")]
    [InlineData("CrestApps.OrchardCore.Transactions")]
    public void ProductsModule_DoesNotDependOnPaymentOrCheckout(string forbiddenPrefix)
    {
        // Products lists and the purchasing modules extend it. A site that only lists products (real estate, a
        // car lot) enables Products without any of them, so the module must never reach back into them.
        AssertNoReferenceTo(typeof(ProductTaxableItemProvider).Assembly, forbiddenPrefix);
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
