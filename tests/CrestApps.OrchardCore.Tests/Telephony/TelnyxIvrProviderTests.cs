using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The half of an entry-point menu that reaches the caller. The flow decides what the menu says and is covered on
/// its own; this asserts the Telnyx command that makes them hear it: <c>gather_using_speak</c> or
/// <c>gather_using_audio</c> (both of which need the call answered first), with the keys the menu offers.
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

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("Press 1 for sales, 2 for support.", body.RootElement.GetProperty("payload").GetString());
        Assert.Equal("12", body.RootElement.GetProperty("valid_digits").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("maximum_digits").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("minimum_digits").GetInt32());
    }

    [Fact]
    public async Task ASpokenMenu_NamesAVoice()
    {
        // Arrange
        // Telnyx requires "voice" on gather_using_speak. Without it the command is refused and the caller hears
        // nothing at all.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.PromptAsync("ctrl-1", "Press 1.", mediaId: null, "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("female", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("en-US", body.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task ASpokenMenu_IsSpokenInTheTenantsVoiceAndLanguage()
    {
        // Arrange
        // The voice used to be fixed; a tenant whose callers speak another language had every menu read in US English.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler, options: new TelnyxOptions { TtsVoice = "AWS.Polly.Lupe-Neural", TtsLanguage = "es-US" });

        // Act
        await provider.PromptAsync("ctrl-1", "Para ventas, oprima 1.", mediaId: null, "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("AWS.Polly.Lupe-Neural", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("es-US", body.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task AMenu_IsCollectedOnce_SoTheFlowDecidesWhatAMissedKeyMeans()
    {
        // Arrange
        // Telnyx replays the prompt up to maximum_tries times (3 by default) and waits a minute each time before
        // reporting that nothing was pressed; with the flow's own retries on top, a caller who missed the menu heard
        // it nine times over several minutes before anything happened.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.PromptAsync("ctrl-1", "Press 1.", mediaId: null, "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(1, body.RootElement.GetProperty("maximum_tries").GetInt32());
        Assert.Equal(8000, body.RootElement.GetProperty("timeout_millis").GetInt32());
    }

    [Fact]
    public async Task AMenuOfferingTheHashKey_DoesNotEndCollectionOnIt()
    {
        // Arrange
        // '#' is Telnyx's default terminating digit, which ends collection with nothing collected; a menu that offers
        // it as an option could never be chosen.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.PromptAsync("ctrl-1", "Press # to repeat.", mediaId: null, "1#", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("*", body.RootElement.GetProperty("terminating_digit").GetString());
    }

    [Fact]
    public async Task ARecordedMenuHostedElsewhere_IsPlayedFromItsUrl()
    {
        // Arrange
        // A tenant who recorded their menu did so because they did not want it read out by a synthesizer.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.PromptAsync("ctrl-1", "Press 1 for sales.", "https://example.test/menu.mp3", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_audio", handler.Requests[0].Path);

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("https://example.test/menu.mp3", body.RootElement.GetProperty("audio_url").GetString());
        Assert.False(body.RootElement.TryGetProperty("payload", out _));
    }

    [Fact]
    public async Task ARecordedMenuFromTheMediaLibrary_IsPlayedByItsTelnyxName()
    {
        // Arrange
        // The menu stores the voice media catalog id, which means nothing to Telnyx. Sent as audio_url it was
        // refused and the caller heard silence; the clip is held under the media_name Telnyx gave it at upload.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler, new VoiceMediaItem { ItemId = "media-1", MediaReference = "tenant-main-menu" });

        // Act
        var played = await provider.PromptAsync("ctrl-1", "Press 1 for sales.", "media-1", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(played);
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_audio", handler.Requests[0].Path);

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("tenant-main-menu", body.RootElement.GetProperty("media_name").GetString());
        Assert.False(body.RootElement.TryGetProperty("audio_url", out _));
    }

    [Fact]
    public async Task ARecordedMenuThatCannotBeFound_FallsBackToSpeakingTheText()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync("ctrl-1", "Press 1 for sales.", "media-deleted", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(played);
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_speak", handler.Requests.Single().Path);
    }

    [Fact]
    public async Task ARecordedMenuThatCannotBeFound_WithNoText_PlaysNothing()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PromptAsync("ctrl-1", text: null, "media-deleted", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(played);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnsweringTheCaller_PostsTheAnswerCommand()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var answered = await provider.AnswerAsync("ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(answered);
        Assert.Equal("/v2/calls/ctrl-1/actions/answer", handler.Requests.Single().Path);
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

    private static TelnyxIvrProvider CreateProvider(HttpMessageHandler handler, VoiceMediaItem media = null, TelnyxOptions options = null)
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

        var mediaManager = new Mock<IVoiceMediaItemManager>();
        mediaManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((id, _) => ValueTask.FromResult(media is not null && id == media.ItemId ? media : null));

        return new TelnyxIvrProvider(
            apiClient,
            mediaManager.Object,
            new TestOptionsMonitor<TelnyxOptions>(options ?? new TelnyxOptions()),
            NullLogger<TelnyxIvrProvider>.Instance);
    }
}
