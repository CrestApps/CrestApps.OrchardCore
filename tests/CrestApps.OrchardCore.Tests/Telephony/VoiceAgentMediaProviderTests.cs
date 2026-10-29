using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The automated voice loop — render the prompt, run the completion, decide whether to hand off, conclude the
/// call — is identical whichever provider carries the audio, but it lived inside the Telnyx module, so a second
/// provider could only offer automated voice by copying a thousand-line handler. These cover the seam that
/// replaces that: four provider methods, behind which everything Telnyx-specific sits.
/// </summary>
public sealed class VoiceAgentMediaProviderTests
{
    [Fact]
    public async Task Speak_ReachesTheProvidersSpeakAction()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var spoke = await provider.SpeakAsync("ctrl-1", "Hello, how can I help?", "female", "en-US", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(spoke);
        Assert.Equal("/v2/calls/ctrl-1/actions/speak", handler.Requests[0].Path);
        Assert.Contains("Hello, how can I help?", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transcription_StartsAndStopsOnTheirOwnActions()
    {
        // Arrange
        // Stopping matters: a caller who is still being transcribed while the assistant speaks has the
        // assistant's own words fed back in as if they had said them.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.StartTranscriptionAsync("ctrl-1", "en", "cmd-1", TestContext.Current.CancellationToken);
        await provider.StopTranscriptionAsync("ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/transcription_start", handler.Requests[0].Path);
        Assert.Equal("/v2/calls/ctrl-1/actions/transcription_stop", handler.Requests[1].Path);
    }

    [Fact]
    public async Task StartingTranscription_ListensToTheFarEndOnly()
    {
        // Arrange
        // Transcribing both tracks feeds the assistant's own text-to-speech back in as if the person had said it,
        // and the conversation then answers itself.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.StartTranscriptionAsync("ctrl-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("\"transcription_tracks\":\"inbound\"", handler.Requests[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartingTranscription_UsesAModelMadeForPhoneAudio()
    {
        // Arrange
        // The untuned default engine heard a caller's answers as fragments and read an email address back wrong
        // four times running, and every turn of the conversation is built on what it hears.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.StartTranscriptionAsync("ctrl-1", "en", "cmd-1", TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Contains("\"transcription_engine\":\"Deepgram\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"transcription_model\":\"deepgram/nova-3\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"smart_format\":true", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartingTranscription_FallsBackToThePhoneCallModel_WhenTheEngineIsRefused()
    {
        // Arrange
        // A call that is not listening is a caller talking to nobody, so a refusal has to lead somewhere.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity)
            .RespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var started = await provider.StartTranscriptionAsync("ctrl-1", "en", "cmd-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("\"model\":\"phone_call\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"command_id\":\"cmd-1-fallback\"", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("\"transcription_tracks\":\"inbound\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProvider_CarriesItsOwnDefaultVoice()
    {
        // Arrange
        // Which voices exist, and what they are called, is a fact about the provider. A neural voice rather than a
        // basic one is most of the difference between a call that sounds like a person and one that does not.
        var provider = CreateProvider(new RecordingHttpMessageHandler());

        // Assert
        Assert.Equal("AWS.Polly.Joanna-Neural", provider.DefaultVoice);
    }

    [Fact]
    public async Task Gather_SpeaksThePromptAndCollectsAKey()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.GatherAsync("ctrl-1", "Press 1 to continue", "1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/v2/calls/ctrl-1/actions/gather_using_speak", handler.Requests[0].Path);
    }

    [Fact]
    public async Task Gather_NamesTheTenantsVoiceAndLanguage()
    {
        // Arrange
        // Telnyx lists "voice" as required on gather_using_speak and refuses the command without it, so a key the AI
        // agent asked for was never collected and the caller heard nothing.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler, new TelnyxOptions { TtsVoice = "AWS.Polly.Lupe-Neural", TtsLanguage = "es-US" });

        // Act
        await provider.GatherAsync("ctrl-1", "Oprima 1 para continuar", "12", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("AWS.Polly.Lupe-Neural", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("es-US", body.RootElement.GetProperty("language").GetString());
        Assert.Equal("12", body.RootElement.GetProperty("valid_digits").GetString());
        Assert.DoesNotContain(body.RootElement.GetProperty("terminating_digit").GetString(), "12", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gather_WithoutATenantVoice_NamesTheDefaultVoice()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        await provider.GatherAsync("ctrl-1", "Press 1 to continue", "1", TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("female", body.RootElement.GetProperty("voice").GetString());
        Assert.Equal("en-US", body.RootElement.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Hangup_EndsTheCall()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var ended = await provider.HangupAsync("ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ended);
        Assert.Equal("/v2/calls/ctrl-1/actions/hangup", handler.Requests[0].Path);
    }

    [Fact]
    public async Task ARefusedCommand_IsReportedRatherThanThrown()
    {
        // Arrange
        // The loop is mid-conversation with a real caller. An exception here abandons them silently; a false
        // lets the loop decide what to do about it.
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity);
        var provider = CreateProvider(handler);

        // Act
        var spoke = await provider.SpeakAsync("ctrl-1", "Hello", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(spoke);
    }

    [Fact]
    public void TheProvider_NamesItself_SoATenantWithSeveralResolvesTheRightOne()
    {
        // Arrange
        var provider = CreateProvider(new RecordingHttpMessageHandler());

        // Assert
        Assert.Equal("Telnyx", provider.TechnicalName);
    }

    [Fact]
    public void TheResolver_ReturnsTheProviderForTheCallInHand()
    {
        // Arrange
        var telnyx = CreateProvider(new RecordingHttpMessageHandler());
        var resolver = new VoiceAgentMediaProviderResolver([telnyx]);

        // Assert
        Assert.Same(telnyx, resolver.Get("Telnyx"));
        Assert.Same(telnyx, resolver.Get("telnyx"));
    }

    [Fact]
    public void TheResolver_ReturnsNothing_ForAProviderThatCannotDoAutomatedVoice()
    {
        // Arrange
        // A tenant on a provider with no automated-voice media should learn that from a null, not from a call
        // that connects and then sits in silence.
        var resolver = new VoiceAgentMediaProviderResolver([CreateProvider(new RecordingHttpMessageHandler())]);

        // Assert
        Assert.Null(resolver.Get("SomeOtherProvider"));
        Assert.Null(resolver.Get(null));
    }

    [Fact]
    public void TheResolver_WithASingleProvider_ResolvesItWhenNoNameIsGiven()
    {
        // Arrange
        // An older call record may carry no provider name. With exactly one provider registered there is no
        // ambiguity about which one it was.
        var telnyx = CreateProvider(new RecordingHttpMessageHandler());
        var resolver = new VoiceAgentMediaProviderResolver([telnyx]);

        // Assert
        Assert.Same(telnyx, resolver.GetDefault());
    }

    [Fact]
    public async Task DialSideCall_RingsTheNumberMarkedAsTheAITransferLegOfTheCaller()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"side-1"}}""");
        var provider = CreateProvider(handler, new TelnyxOptions { IsEnabled = true, ConnectionId = "conn-1", DefaultOutboundCallerId = "+15550001111" });

        // Act
        var sideCallId = await provider.DialSideCallAsync(new VoiceAgentSideCallRequest
        {
            ProviderCallId = "caller-1",
            ActivityId = "act-1",
            PhoneNumber = "+15552223333",
            RingTimeoutSeconds = 20,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("side-1", sideCallId);
        Assert.Equal("/v2/calls", handler.Requests[0].Path);
        Assert.Contains("15552223333", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("answering_machine_detection", handler.Requests[0].Body, StringComparison.Ordinal);

        var clientState = JsonDocument.Parse(handler.Requests[0].Body).RootElement.GetProperty("client_state").GetString();
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientState));
        Assert.True(TelnyxOutboundBridgeState.TryParse(decoded, out var state));
        Assert.Equal(TelnyxOutboundBridgeState.AiTransferLegIntent, state.Intent);
        Assert.Equal("caller-1", state.PeerCallControlId);
        Assert.Equal("act-1", state.ActivityId);
    }

    [Fact]
    public async Task BridgeAndHoldAudio_ReachTheirOwnActions()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        // Act
        var played = await provider.PlayAudioAsync("caller-1", "https://example.com/hold.mp3", cancellationToken: TestContext.Current.CancellationToken);
        var stopped = await provider.StopAudioAsync("caller-1", TestContext.Current.CancellationToken);
        var bridged = await provider.BridgeAsync("caller-1", "side-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(played && stopped && bridged);
        Assert.Equal("/v2/calls/caller-1/actions/playback_start", handler.Requests[0].Path);
        Assert.Equal("/v2/calls/caller-1/actions/playback_stop", handler.Requests[1].Path);
        Assert.Equal("/v2/calls/caller-1/actions/bridge", handler.Requests[2].Path);
        Assert.Contains("side-1", handler.Requests[2].Body, StringComparison.Ordinal);
    }

    private static TelnyxVoiceAgentMediaProvider CreateProvider(HttpMessageHandler handler, TelnyxOptions options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        options ??= new TelnyxOptions();
        options.ApiBaseUrl = "https://api.telnyx.com/v2/";
        options.ApiKey = "test-api-key";

        var apiClient = new TelnyxApiClient(
            httpClient,
            new TestOptionsMonitor<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxVoiceAgentMediaProvider(apiClient, new TestOptionsMonitor<TelnyxOptions>(options));
    }
}
