using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class MoneyTypeContractTests
{
    // The commerce foundation stores every monetary amount as decimal so amounts never suffer binary
    // floating-point drift between editing, taxing, charging, and settlement. This guard fails if a
    // refactor reintroduces double/float on any authoritative money carrier the future e-commerce
    // domain will build orders and ledgers on top of.
    public static TheoryData<Type> MoneyBearingTypes => new()
    {
        typeof(ProductPart),
        typeof(PaymentAttempt),
        typeof(PaymentRecord),
        typeof(BillingItem),
        typeof(CheckoutLineItem),
        typeof(CheckoutInvoice),
        typeof(InvoiceLineItem),
    };

    [Theory]
    [MemberData(nameof(MoneyBearingTypes))]
    public void MoneyBearingContracts_DoNotExposeBinaryFloatingPoint(Type type)
    {
        var offending = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => IsBinaryFloatingPoint(property.PropertyType))
            .Select(property => property.Name)
            .ToArray();

        Assert.True(
            offending.Length == 0,
            $"'{type.Name}' must expose money as decimal, but these properties use double/float: {string.Join(", ", offending)}.");
    }

    private static bool IsBinaryFloatingPoint(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        return target == typeof(double) || target == typeof(float);
    }
}
