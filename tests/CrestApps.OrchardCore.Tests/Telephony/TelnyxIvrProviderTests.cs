using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The half of an entry-point menu that reaches the caller. The flow decides what the menu says and is covered on
/// its own; this asserts the command that makes them hear it, and that the menu only accepts the keys it offers.
/// </summary>
public sealed class TelnyxIvrProviderTests
{
    [Fact]
    public async Task AMenu_IsSpokenAndCollectsOneKey()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync("ctrl-1", "Press 1 for sales, 2 for support.", mediaId: null, "12", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(played);
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_speak", handler.Requests[0].Path);
        Assert.Contains("\"valid_digits\":\"12\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"maximum_digits\":1", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordedMenu_IsPlayedRatherThanRead()
    {
        // Arrange
        // A tenant who recorded their menu did so because they did not want it read out by a synthesizer.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.PromptAsync("ctrl-1", "Press 1 for sales.", "https://example.test/menu.mp3", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_audio", handler.Requests[0].Path);
        Assert.Contains("https://example.test/menu.mp3", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMenuWithNothingToPlay_SendsNothing()
    {
        // Arrange
        // A menu node with neither text nor audio is a configuration mistake, and asking the provider to say
        // nothing just logs an error against a live call.
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync("ctrl-1", text: null, mediaId: null, "12", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(played);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task WithNoCallToPromptOn_NothingIsSent()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync(providerCallId: null, "Press 1.", mediaId: null, "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(played);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ARefusedPrompt_IsReportedRatherThanThrown()
    {
        // Arrange
        // The caller may have hung up while the menu was being decided. The inbound path has to carry on and
        // settle the call, not fail.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity);
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync("ctrl-1", "Press 1.", mediaId: null, "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(played);
    }

    private static TelnyxIvrProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var apiClient = new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(new TelnyxOptions
            {
                ApiBaseUrl = "https://api.telnyx.com/v2/",
                ApiKey = "test-api-key",
            }),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxIvrProvider(apiClient);
    }
}
