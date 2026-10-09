using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// An automated voice agent tells the person it called that the call is recorded before anything else it says, in
/// the tenant's words. Left to the profile's instructions, the notice was only as reliable as the model's reading of
/// them, and every profile had to repeat it.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";

    [Fact]
    public async Task ATurnBasedCall_SpeaksTheDisclosureWordForWord_AheadOfTheGreeting()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // One line, so nothing can come between the disclosure and the greeting.
        var opening = Assert.Single(harness.Media.Spoken);
        Assert.StartsWith(Disclosure + " ", opening, StringComparison.Ordinal);
        Assert.True(opening.Length > Disclosure.Length + 1);
    }

    [Fact]
    public async Task ATurnBasedCall_WithoutADisclosure_OpensWithTheGreetingAlone()
    {
        // Arrange
        // Call recording is on, but automated voice agents are not told to give the disclosure.
        var harness = new LoopHarness();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(null));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(Disclosure, Assert.Single(harness.Media.Spoken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARealtimeCall_IsGivenTheDisclosureForItsOpening()
    {
        // Arrange
        // A live session writes its own opening, so it can only be told to give the disclosure.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Disclosure, Assert.Single(harness.Realtime.Sessions).RecordingDisclosure);
    }

    [Fact]
    public async Task ARealtimeCall_WithoutCallRecording_IsGivenNoDisclosure()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Assert.Single(harness.Realtime.Sessions).RecordingDisclosure);
    }

    [Fact]
    public async Task ATurnBasedCall_RecordsTheDisclosureWithTheWordsUsed()
    {
        // Arrange
        // The platform says it, so the provider accepting the line is the proof.
        var harness = new LoopHarness();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var disclosed = Assert.Single(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosed);
        Assert.Equal(Disclosure, disclosed.RecordingDisclosure);
        Assert.Null(disclosed.OpeningLine);
        Assert.Equal("call-1", disclosed.ProviderCallId);
        Assert.DoesNotContain(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosureMissed);
    }

    [Fact]
    public async Task ARealtimeCallThatOpenedWithTheDisclosure_IsRecordedAsDisclosed()
    {
        // Arrange
        // Transcription keeps neither case nor punctuation reliably, so only the words are compared.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        harness.Realtime.DuringSession = _ => harness.AssistantSaid(
            "this call may be recorded, for quality assurance and training purposes! Hey Jack, this is Sarah.");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var disclosed = Assert.Single(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosed);
        Assert.Equal(Disclosure, disclosed.RecordingDisclosure);
    }

    [Fact]
    public async Task ARealtimeCallThatParaphrasedTheDisclosure_IsRecordedAsMissed_WithWhatWasSaid()
    {
        // Arrange
        // Telling the model to quote the notice is not proof that it did: a paraphrase is not the approved notice.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        harness.Realtime.DuringSession = _ => harness.AssistantSaid("Just so you know, we record calls. Hey Jack!");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var missed = Assert.Single(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosureMissed);
        Assert.Equal(Disclosure, missed.RecordingDisclosure);
        Assert.Equal("Just so you know, we record calls. Hey Jack!", missed.OpeningLine);
        Assert.DoesNotContain(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosed);
    }

    [Fact]
    public async Task ARealtimeCallTheAssistantNeverSpokeOn_IsRecordedAsMissed()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var missed = Assert.Single(harness.CallObserver.Observations, o => o.Kind == AutomatedVoiceCallObservationKind.RecordingDisclosureMissed);
        Assert.Null(missed.OpeningLine);
    }

    [Fact]
    public async Task ACallWithNoDisclosureToGive_RecordsNothingAboutOne()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.DuringSession = _ => harness.AssistantSaid("Hey Jack, this is Sarah.");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(
            harness.CallObserver.Observations,
            o => o.Kind is AutomatedVoiceCallObservationKind.RecordingDisclosed or AutomatedVoiceCallObservationKind.RecordingDisclosureMissed);
    }

    private sealed partial class LoopHarness
    {
        /// <summary>
        /// Stores a line the live session's assistant said, the way the session writes its transcript.
        /// </summary>
        public void AssistantSaid(string line)
            => _prompts.Add(new AIChatSessionPrompt
            {
                Role = ChatRole.Assistant,
                Content = line,
                CreatedUtc = Clock.UtcNow,
            });
    }

    private sealed class FixedDisclosureProvider : IRecordingDisclosureProvider
    {
        private readonly string _aiDisclosure;

        public FixedDisclosureProvider(string aiDisclosure)
        {
            _aiDisclosure = aiDisclosure;
        }

        public Task<string> GetDisclosureAsync(RecordingDisclosureCallType callType, CancellationToken cancellationToken = default)
            => Task.FromResult(callType == RecordingDisclosureCallType.AIVoiceAgent ? _aiDisclosure : null);
    }
}
