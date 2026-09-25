using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
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

        var resolved = await dispatcher.ResolveProviderNameAsync("+15553334444", TestContext.Current.CancellationToken);

        Assert.Equal("Telnyx", resolved);
    }

    [Fact]
    public async Task ResolveProviderName_FallsBackToTenantSmsDefault_WhenNumberHasNoProvider()
    {
        var dispatcher = CreateDispatcher(
            endpointProvider: null,
            smsDefault: "AzureCommunicationServices");

        var resolved = await dispatcher.ResolveProviderNameAsync("+15553334444", TestContext.Current.CancellationToken);

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

        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        provider.Verify(p => p.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553334444"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Fails_WhenNoProviderResolves()
    {
        var dispatcher = CreateDispatcher(endpointProvider: null, smsDefault: null);

        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderSaysTheRecipientOptedOut_RecordsTheContactsOptOut()
    {
        // Arrange
        // A provider with its own opt-out management blocks a number that texted STOP. Every later send to it is
        // refused the same way, so the contact is recorded as opted out, exactly as an inbound STOP records it.
        var provider = new Mock<ISmsProvider>();
        provider.Setup(p => p.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                SmsProviderRefusalScope.Report(OmnichannelConstants.SmsErrorCodes.RecipientOptedOut);

                return Result.Failed(new LocalizedString("unsubscribed", "Attempt to send to unsubscribed recipient"));
            });

        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(m => m.GetAsync("contact-1", It.IsAny<VersionOptions>())).ReturnsAsync(contact);

        var dispatcher = CreateDispatcher("Twilio", smsDefault: null, name => provider.Object, contentManager, contactContentItemId: "contact-1");

        // Act
        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OmnichannelConstants.SmsErrorCodes.RecipientOptedOut, result.ErrorCode);
        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part) && part.DoNotSms);
        contentManager.Verify(m => m.UpdateAsync(contact), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenADispatchProviderReportsTheOptOut_RecordsTheContactsOptOut()
    {
        // Arrange
        var provider = new Mock<ISmsProvider>();
        provider.As<ISmsDispatchProvider>()
            .Setup(p => p.DispatchAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsDispatchResult { Succeeded = false, ErrorCode = OmnichannelConstants.SmsErrorCodes.RecipientOptedOut });

        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(m => m.GetAsync("contact-1", It.IsAny<VersionOptions>())).ReturnsAsync(contact);

        var dispatcher = CreateDispatcher("Telnyx", smsDefault: null, name => provider.Object, contentManager, contactContentItemId: "contact-1");

        // Act
        await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part) && part.DoNotSms);
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderRefusesForAnotherReason_LeavesTheContactAlone()
    {
        // Arrange
        var provider = new Mock<ISmsProvider>();
        provider.Setup(p => p.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failed(new LocalizedString("invalid", "Invalid To")));

        var contentManager = new Mock<IContentManager>();
        var dispatcher = CreateDispatcher("Twilio", smsDefault: null, name => provider.Object, contentManager, contactContentItemId: "contact-1");

        // Act
        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorCode);
        contentManager.Verify(m => m.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    private static SmsDispatcher CreateDispatcher(
        string endpointProvider,
        string smsDefault,
        Func<string, ISmsProvider> resolver = null,
        Mock<IContentManager> contentManager = null,
        string contactContentItemId = null)
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

        var contactResolver = new Mock<ISmsContactResolver>();
        contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(contactContentItemId));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        return new SmsDispatcher(
            endpointManager.Object,
            providerResolver.Object,
            siteService.Object,
            contactResolver.Object,
            (contentManager ?? new Mock<IContentManager>()).Object,
            clock.Object,
            RedactorProviderFactory.Create(),
            NullLogger<SmsDispatcher>.Instance);
    }
}
