using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.BackgroundTasks;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data.Migration;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

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
        new AbstractApiStartup(CreateLocalizer<AbstractApiStartup>()).ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationProvider) &&
            descriptor.KeyedImplementationType == typeof(AbstractApiPhoneNumberVerificationProvider) &&
            Equals(descriptor.ServiceKey, PhoneNumberVerificationsConstants.Providers.AbstractApi));
    }

    [Fact]
    public void VeriphoneStartup_ShouldRegisterProviderAndSettingsDriver()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new VeriphoneStartup(CreateLocalizer<VeriphoneStartup>()).ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationProvider) &&
            descriptor.KeyedImplementationType == typeof(VeriphonePhoneNumberVerificationProvider) &&
            Equals(descriptor.ServiceKey, PhoneNumberVerificationsConstants.Providers.Veriphone));
    }

    [Fact]
    public void TwilioStartup_ShouldRegisterProviderAndSettingsDriver()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new TwilioStartup(CreateLocalizer<TwilioStartup>()).ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPhoneNumberVerificationProvider) &&
            descriptor.KeyedImplementationType == typeof(TwilioPhoneNumberVerificationProvider) &&
            Equals(descriptor.ServiceKey, PhoneNumberVerificationsConstants.Providers.Twilio));
    }

    [Fact]
    public void OmnichannelContactVerificationStartup_ShouldRegisterContentHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new OmnichannelContactVerificationStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IContentHandler) &&
            descriptor.ImplementationType?.Name == "OmnichannelContactPhoneNumberVerificationHandler");
    }

    private static IStringLocalizer<T> CreateLocalizer<T>()
    {
        var localizer = new Mock<IStringLocalizer<T>>();

        localizer
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        return localizer.Object;
    }
}
