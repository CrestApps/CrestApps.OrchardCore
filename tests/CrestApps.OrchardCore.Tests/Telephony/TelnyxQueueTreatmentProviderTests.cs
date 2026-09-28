using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.Telnyx;

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
        using var body = System.Text.Json.JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("Press 1 and we will call you back.", body.RootElement.GetProperty("payload").GetString());

        // Any key is an answer: a caller who presses something else has said no, and goes straight back to their music.
        Assert.Contains("1", body.RootElement.GetProperty("valid_digits").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, body.RootElement.GetProperty("maximum_digits").GetInt32());
        Assert.NotEqual("1", body.RootElement.GetProperty("terminating_digit").GetString());
    }

    [Fact]
    public async Task ACallbackOffer_NamesTheTenantsVoice_BecauseTelnyxRefusesItWithoutOne()
    {
        // Arrange
        // gather_using_speak lists voice and payload as required. The offer was sent with no voice, Telnyx refused
        // it, and the caller never heard they could be called back.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler, options: new TelnyxOptions { TtsVoice = "AWS.Polly.Joanna-Neural", TtsLanguage = "en-GB" });

        // Act
        await provider.OfferChoiceAsync("ctrl-1", "Press 1 and we will call you back.", "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = System.Text.Json.JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("AWS.Polly.Joanna-Neural", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("en-GB", body.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task ACallbackOffer_IsAskedOnce_AndGivesUpQuickly()
    {
        // Arrange
        // Telnyx replays a prompt three times and waits a minute by default: to a caller who has decided to keep
        // waiting that is the queue nagging them with the music off.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.OfferChoiceAsync("ctrl-1", "Press 1 and we will call you back.", "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = System.Text.Json.JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(1, body.RootElement.GetProperty("maximum_tries").GetInt32());
        Assert.Equal(TelnyxConstants.Gather.TimeoutMillis, body.RootElement.GetProperty("timeout_millis").GetInt32());
    }

    [Fact]
    public async Task AnAnnouncement_NamesTheTenantsVoice()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler, options: new TelnyxOptions { TtsVoice = "male", TtsLanguage = "fr-CA" });

        // Act
        await provider.SpeakAsync("ctrl-1", "Vous êtes le deuxième.", TestContext.Current.CancellationToken);

        // Assert
        using var body = System.Text.Json.JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("male", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("fr-CA", body.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task ARingingTone_IsPlayedFromTheAudioItself_Looped()
    {
        // Arrange
        // For a caller the menu answered who is waiting on an agent with no music to hear: the network's ringback
        // ended when the menu answered, so without this they heard nothing.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.StartRingbackAsync("ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/playback_start", handler.Requests[0].Path);
        using var body = System.Text.Json.JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("infinity", body.RootElement.GetProperty("loop").GetString());
        var wav = Convert.FromBase64String(body.RootElement.GetProperty("playback_content").GetString());
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
        Assert.False(body.RootElement.TryGetProperty("audio_url", out _));
    }

    [Fact]
    public void TheRingingTone_IsTwoSecondsOfToneAndFourOfSilence()
    {
        // Arrange
        var wav = TelnyxRingbackTone.CreateWav();

        // Act
        var sampleRate = BitConverter.ToInt32(wav, 24);
        var samples = BitConverter.ToInt32(wav, 40);
        var toneEnergy = Enumerable.Range(44, sampleRate).Count(index => Math.Abs(wav[index] - 128) > 10);
        var silenceEnergy = Enumerable.Range(44 + (sampleRate * 3), sampleRate).Count(index => Math.Abs(wav[index] - 128) > 1);

        // Assert
        Assert.Equal(8000, sampleRate);
        Assert.Equal(8000 * 6, samples);
        Assert.Equal(44 + samples, wav.Length);
        Assert.True(toneEnergy > sampleRate / 2, "The first second should be the ringing tone.");
        Assert.Equal(0, silenceEnergy);
    }

    [Fact]
    public async Task ALastMessage_IsSpokenMarkedSoTheCallEndsWhenItFinishes()
    {
        // Arrange
        // Hanging up straight after asking Telnyx to speak would cut the confirmation off; the hang-up is issued
        // from call.speak.ended, which carries this state back.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.EndWithMessageAsync("ctrl-1", "We will call you back.", TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v2/calls/ctrl-1/actions/speak", request.Path);
        using var body = System.Text.Json.JsonDocument.Parse(request.Body);
        Assert.Equal("female", body.RootElement.GetProperty("voice").GetString());
        var state = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body.RootElement.GetProperty("client_state").GetString()));
        Assert.True(TelnyxCallFlowClientState.TryParse(state, out var parsed));
        Assert.Equal(TelnyxCallFlowClientState.HangUpAfterSpeechIntent, parsed.Intent);
    }

    [Fact]
    public async Task ALastMessageThatCannotBeSpoken_EndsTheCallAtOnce()
    {
        // Arrange
        // Nothing would ever report the speech finished, so the caller would sit on a silent line.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90018"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.EndWithMessageAsync("ctrl-1", "We will call you back.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["/v2/calls/ctrl-1/actions/speak", "/v2/calls/ctrl-1/actions/hangup"], handler.Requests.Select(request => request.Path));
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

    private static TelnyxQueueTreatmentProvider CreateProvider(HttpMessageHandler handler, Dictionary<string, VoiceMediaItem> catalog = null, TelnyxOptions options = null)
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

        return new TelnyxQueueTreatmentProvider(
            apiClient,
            voiceMedia.Object,
            new TestOptionsMonitor<TelnyxOptions>(options ?? new TelnyxOptions()),
            NullLogger<TelnyxQueueTreatmentProvider>.Instance);
    }
}
