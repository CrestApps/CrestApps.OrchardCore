using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// What a caller waiting in a queue actually hears. The policy decides what is due; this is the half that makes
/// it reach them, and until it existed the queue treatment a tenant configured was silence.
/// </summary>
public sealed class TelnyxQueueTreatmentProviderTests
{
    [Fact]
    public async Task AnAnnouncement_IsSpokenOnTheCallersLeg()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.SpeakAsync("ctrl-1", "You are second in line.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/speak", handler.Requests[0].Path);
        Assert.Contains("You are second in line.", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HoldMusic_LoopsUntilSomebodyAnswers()
    {
        // Arrange
        // Music that plays once leaves the caller in silence for the rest of their wait, which is
        // indistinguishable from a dropped call.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.StartHoldMusicAsync("ctrl-1", "https://example.test/hold.mp3", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/playback_start", handler.Requests[0].Path);
        Assert.Contains("\"loop\":\"infinity\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HoldMusic_WithNoMediaConfigured_PlaysNothingRatherThanFailing()
    {
        // Arrange
        // A queue with no hold media is a queue that wants silence, not a queue that should log a provider error
        // every time somebody waits in it.
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        await provider.StartHoldMusicAsync("ctrl-1", mediaId: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ACallbackOffer_SpeaksThePromptAndListensForTheKey()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.OfferChoiceAsync("ctrl-1", "Press 1 and we will call you back.", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_speak", handler.Requests[0].Path);
        Assert.Contains("\"valid_digits\":\"1\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusedCommand_DoesNotThrowAtTheCaller()
    {
        // Arrange
        // Treatment runs on a timer over every waiting caller. One leg that has just hung up must not take the
        // sweep down and leave everybody else in silence.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity);
        var provider = CreateProvider(handler);

        // Act
        var exception = await Record.ExceptionAsync(
            () => provider.SpeakAsync("ctrl-1", "You are second in line.", TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task WithNoCallToActOn_NothingIsSent()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        await provider.SpeakAsync(providerCallId: null, "Anybody there?", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task HoldMusic_FromTheCatalog_IsPlayedByItsProviderName()
    {
        // Arrange
        // The queue stores the catalog identifier, which means nothing to Telnyx — the clip lives in Telnyx's own
        // storage under the name it was given when uploaded. Passing the catalog id straight through was refused,
        // and a refused playback is silence, so the queue looked correctly configured and the caller heard
        // nothing at all.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, new Dictionary<string, VoiceMediaItem>(StringComparer.Ordinal)
        {
            ["media-1"] = new VoiceMediaItem { ItemId = "media-1", MediaReference = "cc-voice-media-abc123" },
        });

        // Act
        await provider.StartHoldMusicAsync("ctrl-1", "media-1", TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Contains("cc-voice-media-abc123", request.Body, StringComparison.Ordinal);
        Assert.Contains("media_name", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HoldMusic_ReferringToAClipThatIsNotInTheCatalog_PlaysNothing()
    {
        // Arrange
        // Better to leave the caller in silence than to send the provider a name it will reject; the warning is
        // what tells an operator their queue points at a clip that no longer exists.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, []);

        // Act
        await provider.StartHoldMusicAsync("ctrl-1", "media-gone", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(handler.Requests);
    }

    private static TelnyxQueueTreatmentProvider CreateProvider(HttpMessageHandler handler, Dictionary<string, VoiceMediaItem> catalog = null)
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

        var voiceMedia = new Mock<IVoiceMediaItemManager>();
        voiceMedia.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(
                catalog is not null && catalog.TryGetValue(id, out var item) ? item : null));

        return new TelnyxQueueTreatmentProvider(apiClient, voiceMedia.Object, NullLogger<TelnyxQueueTreatmentProvider>.Instance);
    }
}
