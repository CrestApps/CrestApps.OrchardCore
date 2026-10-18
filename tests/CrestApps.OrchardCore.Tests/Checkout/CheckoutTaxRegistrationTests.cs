using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// With Taxation on, the checkout charges tax whichever order its startups run in. The order depends on which other
/// features a tenant has on, so a no-op tax service registered after the real one silently stopped all tax.
/// </summary>
public sealed class CheckoutTaxRegistrationTests
{
    [Fact]
    public void TheTaxationAwareService_Wins_WhenTheTaxationStartupRunsFirst()
        => Assert.Equal(typeof(CheckoutTaxService), Resolve(new CrestApps.OrchardCore.Checkout.TaxationStartup(), new CrestApps.OrchardCore.Checkout.Startup()));

    [Fact]
    public void TheTaxationAwareService_Wins_WhenTheTaxationStartupRunsLast()
        => Assert.Equal(typeof(CheckoutTaxService), Resolve(new CrestApps.OrchardCore.Checkout.Startup(), new CrestApps.OrchardCore.Checkout.TaxationStartup()));

    private static Type Resolve(params StartupBase[] startups)
    {
        var services = new ServiceCollection();

        foreach (var startup in startups)
        {
            startup.ConfigureServices(services);
        }

        return Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICheckoutTaxService)).ImplementationType;
    }
}
