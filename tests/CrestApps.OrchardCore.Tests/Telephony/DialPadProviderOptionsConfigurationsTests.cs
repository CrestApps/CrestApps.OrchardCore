using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DialPadProviderOptionsConfigurationsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Configure_RegistersDialPadProvider_WithEnabledStateFromSettings(bool enabled)
    {
        // Arrange
        var siteService = SiteServiceFactory.Create(new DialPadSettings { IsEnabled = enabled });
        var configuration = new DialPadProviderOptionsConfigurations(siteService);
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.True(options.Providers.ContainsKey(DialPadConstants.ProviderTechnicalName));

        var typeOptions = options.Providers[DialPadConstants.ProviderTechnicalName];
        Assert.Equal(typeof(DialPadTelephonyProvider), typeOptions.Type);
        Assert.Equal(enabled, typeOptions.IsEnabled);
    }
}
