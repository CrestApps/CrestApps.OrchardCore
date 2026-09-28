using CrestApps.OrchardCore.Dialpad;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DialpadProviderOptionsConfigurationsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Configure_RegistersDialpadProvider_WithEnabledStateFromResolvedSettings(bool enabled)
    {
        // Arrange
        var resolvedOptions = new TestOptionsMonitor<DialpadOptions>(new DialpadOptions
        {
            IsEnabled = enabled,
        });
        var configuration = new DialpadProviderOptionsConfigurations(resolvedOptions);
        var options = new TelephonyProviderOptions();

        // Act
        configuration.Configure(options);

        // Assert
        Assert.True(options.Providers.ContainsKey(DialpadConstants.ProviderTechnicalName));

        var typeOptions = options.Providers[DialpadConstants.ProviderTechnicalName];
        Assert.Equal(typeof(DialpadTelephonyProvider), typeOptions.Type);
        Assert.Equal(enabled, typeOptions.IsEnabled);
    }
}
