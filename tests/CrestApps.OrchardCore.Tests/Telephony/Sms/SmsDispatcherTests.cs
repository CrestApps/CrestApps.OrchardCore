using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Infrastructure;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsDispatcherTests
{
    [Fact]
    public async Task ResolveProviderName_PrefersTheNumbersPinnedProvider()
    {
        var dispatcher = CreateDispatcher(
            endpointProvider: "Telnyx",
            smsDefault: "AzureCommunicationServices");

        var resolved = await dispatcher.ResolveProviderNameAsync("+15553334444");

        Assert.Equal("Telnyx", resolved);
    }

    [Fact]
    public async Task ResolveProviderName_FallsBackToTenantSmsDefault_WhenNumberHasNoProvider()
    {
        var dispatcher = CreateDispatcher(
            endpointProvider: null,
            smsDefault: "AzureCommunicationServices");

        var resolved = await dispatcher.ResolveProviderNameAsync("+15553334444");

        Assert.Equal("AzureCommunicationServices", resolved);
    }

    [Fact]
    public async Task SendAsync_RoutesThroughTheResolvedProvider()
    {
        var provider = new Mock<ISmsProvider>();
        provider.Setup(p => p.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var dispatcher = CreateDispatcher(
            endpointProvider: "Telnyx",
            smsDefault: null,
            resolver: name => name == "Telnyx" ? provider.Object : null);

        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" });

        Assert.True(result.Succeeded);
        provider.Verify(p => p.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553334444"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Fails_WhenNoProviderResolves()
    {
        var dispatcher = CreateDispatcher(endpointProvider: null, smsDefault: null);

        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" });

        Assert.False(result.Succeeded);
    }

    private static SmsDispatcher CreateDispatcher(
        string endpointProvider,
        string smsDefault,
        Func<string, ISmsProvider> resolver = null)
    {
        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager.Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelChannelEndpoint { Channel = "SMS", Value = "+15553334444", ProviderName = endpointProvider });

        var providerResolver = new Mock<ISmsProviderResolver>();
        providerResolver.Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => resolver?.Invoke(name));

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<SmsSettings>()).Returns(new SmsSettings { DefaultProviderName = smsDefault });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new SmsDispatcher(endpointManager.Object, providerResolver.Object, siteService.Object, NullLogger<SmsDispatcher>.Instance);
    }
}
