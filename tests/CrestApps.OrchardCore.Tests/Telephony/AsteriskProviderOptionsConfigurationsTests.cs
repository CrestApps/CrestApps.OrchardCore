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
    public void Configure_WhenDefaultAsteriskIsConfiguredOnNonDefaultShell_DoesNotRegisterDefaultProvider()
    {
        // Arrange
        // The host-level default connection is a single shared ARI application. Registering it in a non-default
        // tenant would let that tenant borrow the shared connection and cross-deliver Stasis events, so the default
        // provider must only ever be registered on the default shell.
        var siteService = SiteServiceFactory.Create(new AsteriskSettings());
        var configuration = new AsteriskProviderOptionsConfigurations(
            siteService,
            Options.Create(new DefaultAsteriskOptions { IsEnabled = true }),
            new ShellSettings { Name = "TenantA" });
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.False(options.Providers.ContainsKey(AsteriskConstants.DefaultProviderTechnicalName));
    }
}
