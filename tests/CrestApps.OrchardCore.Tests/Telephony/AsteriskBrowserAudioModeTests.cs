using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskBrowserAudioModeTests
{
    [Fact]
    public void Resolve_WhenAsteriskWebRtcSettingsAreConfigured_ReturnsBrowser()
    {
        // Arrange
        var provider = CreateProvider(new AsteriskSettings
        {
            IsEnabled = true,
            WebSocketUrl = "wss://pbx.example.test/ws",
            SipDomain = "pbx.example.test",
            WebRtcCodecs = "opus,g722,ulaw",
            PjsipCredentialLifetimeMinutes = 15,
            PjsipContactExpirationSeconds = 120,
            PjsipRealtimeProviderInvariantName = "Microsoft.Data.Sqlite",
            PjsipRealtimeConnectionString = "Data Source=asterisk.db",
        });

        // Act
        var result = TelephonyAudioModeResolver.Resolve(
            provider.AudioCapabilities,
            provider.ConfiguredAudioMode,
            provider.BrowserMediaAdapterName);

        // Assert
        Assert.Equal(TelephonyAudioMode.Browser, result);
        Assert.Equal(AsteriskConstants.BrowserMediaAdapterName, provider.BrowserMediaAdapterName);
    }

    [Fact]
    public void Resolve_WhenAsteriskWebRtcSettingsAreIncomplete_FailsClosed()
    {
        // Arrange
        var provider = CreateProvider(new AsteriskSettings
        {
            IsEnabled = true,
            WebSocketUrl = "ws://pbx.example.test/ws",
            SipDomain = "pbx.example.test",
        });

        // Act
        var result = TelephonyAudioModeResolver.Resolve(
            provider.AudioCapabilities,
            provider.ConfiguredAudioMode,
            provider.BrowserMediaAdapterName);

        // Assert
        Assert.Equal(TelephonyAudioMode.None, result);
    }

    private static AsteriskTelephonyProvider CreateProvider(AsteriskSettings settings)
    {
        return new AsteriskTelephonyProvider(
            SiteServiceFactory.Create(settings),
            Mock.Of<IDataProtectionProvider>(),
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IAsteriskAriApplicationGate>(),
            Mock.Of<IClock>(),
            NullLogger<AsteriskTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<AsteriskTelephonyProvider>());
    }
}
