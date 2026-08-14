using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Navigation;
using YesSql.Indexes;

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

    [Fact]
    public void LocalDncRegistryStartup_ShouldRegisterRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new LocalDncRegistryStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INationalDoNotCallRegistry) &&
            descriptor.ImplementationType == typeof(LocalDncRegistry));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ILocalDncListManager) &&
            descriptor.ImplementationType?.Name == "DefaultLocalDncListManager");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ILocalDncFileStore));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "LocalDncRegistryAdminMenu");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDataMigration) &&
            descriptor.ImplementationType?.Name == "LocalDncRegistryMigrations");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIndexProvider) &&
            descriptor.ImplementationType?.Name == "LocalDncListIndexProvider");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIndexProvider) &&
            descriptor.ImplementationType?.Name == "LocalDncEntryIndexProvider");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBackgroundTask) &&
            descriptor.ImplementationType == typeof(LocalDncImportBackgroundTask));
    }
}
