using System.Net;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;
using OrchardCore.Sms;
using OrchardCore.Sms.Models;
using OrchardCore.Sms.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// OrchardCore's Twilio provider carries text only, so pictures on a Twilio number go through this sender, on the same
/// account the tenant configured for Twilio.
/// </summary>
public sealed class TwilioSmsMediaSenderTests
{
    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    [Fact]
    public async Task SendAsync_PostsEachPictureAsAMediaUrl_WithTheAccountsCredentials()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Created, """{"sid":"MM123"}""");
        var sender = CreateSender(handler);

        var result = await sender.SendAsync(
            new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "the form" },
            ["https://site.test/a/image.jpg", "https://site.test/b/image.png"],
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("MM123", result.ProviderMessageId);
        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json", handler.LastRequest.RequestUri.ToString());
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Contains("MediaUrl=https%3A%2F%2Fsite.test%2Fa%2Fimage.jpg", handler.LastRequestBody);
        Assert.Contains("MediaUrl=https%3A%2F%2Fsite.test%2Fb%2Fimage.png", handler.LastRequestBody);
        Assert.Contains("Body=the+form", handler.LastRequestBody);
    }

    [Fact]
    public async Task SendAsync_WhenTwilioSaysTheRecipientUnsubscribed_ReportsTheOptOut()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadRequest, """{"code":21610,"message":"Attempt to send to unsubscribed recipient"}""");

        var result = await CreateSender(handler).SendAsync(
            new SmsMessage { From = "+15553334444", To = "+15551112222" },
            ["https://site.test/a/image.jpg"],
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(OmnichannelConstants.SmsErrorCodes.RecipientOptedOut, result.ErrorCode);
    }

    [Fact]
    public async Task TryAuthenticateAsync_SignsOnlyRequestsToTwilio()
    {
        // The account's credentials must never be handed to whatever host a media address names.
        var sender = CreateSender(new StubHttpMessageHandler(HttpStatusCode.OK));

        using var twilio = new HttpRequestMessage(HttpMethod.Get, "https://api.twilio.com/2010-04-01/Accounts/AC123/Messages/MM1/Media/ME1");
        using var other = new HttpRequestMessage(HttpMethod.Get, "https://media.elsewhere.test/ME1");

        Assert.True(await sender.TryAuthenticateAsync(twilio, TestContext.Current.CancellationToken));
        Assert.NotNull(twilio.Headers.Authorization);
        Assert.False(await sender.TryAuthenticateAsync(other, TestContext.Current.CancellationToken));
        Assert.Null(other.Headers.Authorization);
    }

    private TwilioSmsMediaSender CreateSender(HttpMessageHandler handler)
    {
        var settings = new TwilioSettings
        {
            AccountSID = "AC123",
            AuthToken = _dataProtection.CreateProtector(TwilioSmsProvider.ProtectorName).Protect("secret"),
            PhoneNumber = "+15553334444",
        };

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<TwilioSettings>()).Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new TwilioSmsMediaSender(
            siteService.Object,
            _dataProtection,
            new StubHttpClientFactory(handler),
            NullLogger<TwilioSmsMediaSender>.Instance);
    }
}
