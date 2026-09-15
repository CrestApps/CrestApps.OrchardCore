using System.Net;
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

    private static TelnyxVoiceAgentMediaProvider CreateProvider(HttpMessageHandler handler)
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

        return new TelnyxVoiceAgentMediaProvider(apiClient);
    }
}
