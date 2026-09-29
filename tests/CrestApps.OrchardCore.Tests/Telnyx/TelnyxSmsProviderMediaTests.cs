using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Telnyx;

/// <summary>
/// A picture message goes to Telnyx as media links on the same messages call; Telnyx fetches each picture itself.
/// </summary>
public sealed class TelnyxSmsProviderMediaTests
{
    [Fact]
    public async Task DispatchAsync_WithPictures_SendsTheirLinksAsMediaUrls()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"data":{"id":"msg-1"}}""");
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.DispatchAsync(
            Message(),
            ["https://site.test/messaging/attachments/a/image.jpg", "https://site.test/messaging/attachments/b/image.png"],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("msg-1", result.ProviderMessageId);

        using var body = JsonDocument.Parse(handler.LastRequestBody);
        var media = body.RootElement.GetProperty("media_urls").EnumerateArray().Select(item => item.GetString()).ToArray();

        Assert.Equal(["https://site.test/messaging/attachments/a/image.jpg", "https://site.test/messaging/attachments/b/image.png"], media);
    }

    [Fact]
    public async Task DispatchAsync_WithoutPictures_SendsNoMediaUrls()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"data":{"id":"msg-1"}}""");
        var provider = CreateProvider(handler);

        // Act
        await provider.DispatchAsync(Message(), TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.LastRequestBody);

        Assert.False(body.RootElement.TryGetProperty("media_urls", out _));
    }

    private static SmsMessage Message()
        => new() { From = "+15553334444", To = "+15551112222", Body = "hi" };

    private static TelnyxSmsProvider CreateProvider(HttpMessageHandler handler)
        => new(
            new StubHttpClientFactory(handler),
            new TestOptionsMonitor<TelnyxSmsOptions>(new TelnyxSmsOptions { IsEnabled = true, ApiKey = "key" }),
            NullLogger<TelnyxSmsProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxSmsProvider>());
}
