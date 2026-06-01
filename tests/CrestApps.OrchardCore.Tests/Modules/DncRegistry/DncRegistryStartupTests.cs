using CrestApps.OrchardCore.DncRegistry;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Navigation;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

public sealed class DncRegistryStartupTests
{
    [Fact]
    public void BaseStartup_ShouldRegisterOnlySharedNavigationProvider()
    {
        var services = new ServiceCollection();

        new Startup().ConfigureServices(services);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "DncRegistryAdminMenu");
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "UsaFtcDncRegistryAdminMenu");
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "CanadaDnclRegistryAdminMenu");
    }

    [Fact]
    public void FeatureStartups_ShouldRegisterFeatureSpecificNavigationProviders()
    {
        var usaServices = new ServiceCollection();
        var canadaServices = new ServiceCollection();

        new UsaFtcStartup().ConfigureServices(usaServices);
        new CanadaDnclStartup().ConfigureServices(canadaServices);

        Assert.Contains(usaServices, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "UsaFtcDncRegistryAdminMenu");
        Assert.Contains(canadaServices, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "CanadaDnclRegistryAdminMenu");
    }
}
