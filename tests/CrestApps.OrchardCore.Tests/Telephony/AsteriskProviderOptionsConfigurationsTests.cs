using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskProviderOptionsConfigurationsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Configure_RegistersTenantAsteriskProvider_WithEnabledStateFromSettings(bool enabled)
    {
        // Arrange
        var siteService = SiteServiceFactory.Create(new AsteriskSettings
        {
            IsEnabled = enabled,
            BaseUrl = "http://localhost:8088/ari/",
            UserName = "ari-user",
            Password = "protected-password",
            ApplicationName = "crestapps-telephony",
        });
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions()),
            new ShellSettings { Name = "Default" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.True(options.Providers.ContainsKey(AsteriskConstants.ProviderTechnicalName));

        var typeOptions = options.Providers[AsteriskConstants.ProviderTechnicalName];
        Assert.Equal(typeof(AsteriskTelephonyProvider), typeOptions.Type);
        Assert.Equal(enabled, typeOptions.IsEnabled);
    }

    [Fact]
    public void Configure_WhenTenantAsteriskSettingsAreIncomplete_DisablesTenantProvider()
    {
        // Arrange
        var siteService = SiteServiceFactory.Create(new AsteriskSettings { IsEnabled = true });
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions()),
            new ShellSettings { Name = "Default" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.False(options.Providers[AsteriskConstants.ProviderTechnicalName].IsEnabled);
    }

    [Fact]
    public void Configure_WhenDefaultAsteriskIsConfigured_RegistersDefaultProvider()
    {
        // Arrange
        var siteService = SiteServiceFactory.Create(new AsteriskSettings());
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions { IsEnabled = true }),
            new ShellSettings { Name = "Default" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.True(options.Providers.ContainsKey(AsteriskConstants.DefaultProviderTechnicalName));

        var typeOptions = options.Providers[AsteriskConstants.DefaultProviderTechnicalName];
        Assert.Equal(typeof(DefaultAsteriskTelephonyProvider), typeOptions.Type);
        Assert.True(typeOptions.IsEnabled);
    }

    [Fact]
    public void Configure_WhenDefaultAsteriskIsConfiguredOnNonDefaultShell_RegistersDefaultProvider()
    {
        // Arrange
        // A host-configured default connection is a shared provider that every tenant may select. Each shell resolves
        // it under a unique per-tenant ARI application name at runtime, so registering it on a non-default shell does
        // not cross-deliver Stasis events between tenants.
        var siteService = SiteServiceFactory.Create(new AsteriskSettings());
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions { IsEnabled = true }),
            new ShellSettings { Name = "TenantA" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.True(options.Providers.ContainsKey(AsteriskConstants.DefaultProviderTechnicalName));
        Assert.True(options.Providers[AsteriskConstants.DefaultProviderTechnicalName].IsEnabled);
    }

    [Fact]
    public void Configure_WhenDefaultAsteriskIsNotConfigured_DoesNotRegisterDefaultProvider()
    {
        // Arrange
        var siteService = SiteServiceFactory.Create(new AsteriskSettings());
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions { IsEnabled = false }),
            new ShellSettings { Name = "Default" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.False(options.Providers.ContainsKey(AsteriskConstants.DefaultProviderTechnicalName));
    }
}
