using CrestApps.OrchardCore.PhoneNumberVerifications;
using CrestApps.OrchardCore.PhoneNumberVerifications.BackgroundTasks;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumberVerifications;

public sealed class PhoneNumberVerificationsStartupTests
{
    [Fact]
    public void Startup_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationManager) &&
            descriptor.ImplementationType == typeof(DefaultPhoneNumberVerificationManager));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationStore) &&
            descriptor.ImplementationType == typeof(ContentItemPhoneNumberVerificationStore));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IContentPhoneNumberResolver) &&
            descriptor.ImplementationType == typeof(DefaultContentPhoneNumberResolver));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IIndexProvider) &&
            descriptor.ImplementationType?.Name == "PhoneNumberVerificationPartIndexProvider");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDataMigration) &&
            descriptor.ImplementationType?.Name == "PhoneNumberVerificationsMigrations");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBackgroundTask) &&
            descriptor.ImplementationType == typeof(PhoneNumberRevalidationBackgroundTask));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(INavigationProvider) &&
            descriptor.ImplementationType?.Name == "PhoneNumberVerificationsAdminMenu");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPermissionProvider) &&
            descriptor.ImplementationType?.Name == "PhoneNumberVerificationsPermissionProvider");
    }

    [Fact]
    public void Startup_ShouldNotRegisterAnyProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new Startup().ConfigureServices(services);

        // Assert
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationProvider));
    }

    [Fact]
    public void AbstractApiStartup_ShouldRegisterProviderAndSettingsDriver()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new AbstractApiStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationProvider) &&
            descriptor.KeyedImplementationType == typeof(AbstractApiPhoneNumberVerificationProvider) &&
            Equals(descriptor.ServiceKey, PhoneNumberVerificationsConstants.Providers.AbstractApi));
    }
}
