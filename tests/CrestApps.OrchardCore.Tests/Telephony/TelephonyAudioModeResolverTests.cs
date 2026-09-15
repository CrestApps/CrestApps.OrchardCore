using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyAudioModeResolverTests
{
    [Theory]
    [InlineData(TelephonyAudioCapabilities.Browser, TelephonyAudioMode.None, "adapter", TelephonyAudioMode.Browser)]
    [InlineData(TelephonyAudioCapabilities.ExternalDevice, TelephonyAudioMode.None, null, TelephonyAudioMode.ExternalDevice)]
    [InlineData(TelephonyAudioCapabilities.Browser | TelephonyAudioCapabilities.ExternalDevice, TelephonyAudioMode.Browser, "adapter", TelephonyAudioMode.Browser)]
    [InlineData(TelephonyAudioCapabilities.Browser | TelephonyAudioCapabilities.ExternalDevice, TelephonyAudioMode.ExternalDevice, "adapter", TelephonyAudioMode.ExternalDevice)]
    [InlineData(TelephonyAudioCapabilities.Browser | TelephonyAudioCapabilities.ExternalDevice, TelephonyAudioMode.None, "adapter", TelephonyAudioMode.None)]
    [InlineData(TelephonyAudioCapabilities.Browser, TelephonyAudioMode.Browser, null, TelephonyAudioMode.None)]
    public void Resolve_GivenCapabilitiesAndConfiguration_ReturnsExecutableMode(
        TelephonyAudioCapabilities capabilities,
        TelephonyAudioMode configuredMode,
        string browserMediaAdapterName,
        TelephonyAudioMode expected)
    {
        // Act
        var result = TelephonyAudioModeResolver.Resolve(capabilities, configuredMode, browserMediaAdapterName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_WhenAsteriskBrowserAdapterConfigured_ReturnsBrowser()
    {
        // Act
        var result = TelephonyAudioModeResolver.Resolve(
            TelephonyAudioCapabilities.Browser,
            TelephonyAudioMode.Browser,
            "sipjs");

        // Assert
        Assert.Equal(TelephonyAudioMode.Browser, result);
    }

    [Fact]
    public void Resolve_WhenAsteriskBrowserAdapterMissing_FailsClosed()
    {
        // Act
        var result = TelephonyAudioModeResolver.Resolve(
            TelephonyAudioCapabilities.Browser,
            TelephonyAudioMode.Browser,
            null);

        // Assert
        Assert.Equal(TelephonyAudioMode.None, result);
    }
}
