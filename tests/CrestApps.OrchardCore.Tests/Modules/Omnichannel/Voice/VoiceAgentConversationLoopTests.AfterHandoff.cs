using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What the assistant does once a turn-based call has been handed to a person: nothing at all.
/// </summary>
/// <remarks>
/// Live, the assistant announced the transfer, the agent was bridged onto the customer's leg, and the assistant
/// carried on listening and talking on that same leg: it transcribed the customer talking to the agent, answered
/// them, asked for a transfer again and announced "connecting you" again, with all three of them on one call until
/// the agent hung up. A realtime session ends at the handoff; the turn-based loop never did, because the end of
/// the announcement raised a speak.ended like any other line, and that handler starts listening.
/// </remarks>
public sealed partial class VoiceAgentConversationLoopTests
{
    public static TheoryData<HandoffDisposition> AcceptedHandoffs => new()
    {
        HandoffDisposition.Routed,
        HandoffDisposition.WaitingInQueue,
    };

    [Theory]
    [MemberData(nameof(AcceptedHandoffs))]
    public async Task OnceTheCallerIsHandedToAnAgent_TheAssistantNeverListensAgain(HandoffDisposition disposition)
    {
        // Arrange
        var harness = await HandedOffAsync(disposition);
        var starts = harness.Media.TranscriptionStarts;
        var watches = harness.SilenceWatchdog.Armed.Count;
        var spoken = harness.Media.Spoken.Count;

        // Act
        // The announcement of the transfer finishes. This is the speak.ended that used to start listening again.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(starts, harness.Media.TranscriptionStarts);
        Assert.Equal(watches, harness.SilenceWatchdog.Armed.Count);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Theory]
    [MemberData(nameof(AcceptedHandoffs))]
    public async Task WhatIsHeardAfterTheHandoff_IsNotAnsweredByTheAssistant(HandoffDisposition disposition)
    {
        // Arrange
        // The model still asks for a transfer on every turn it is given, exactly as it did live: once the caller
        // is talking to the agent, "connect me" reads to it as a caller still waiting to be connected.
        var harness = await HandedOffAsync(disposition);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        var completions = harness.Completions;
        var spoken = harness.Media.Spoken.Count;
        var stops = harness.Media.TranscriptionStops;
        var turns = harness.PromptCount;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Hi, yes, I was waiting to be connected.", cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // The model is not asked, nothing is said, and nothing is added to the assistant's transcript.
        Assert.Equal(completions, harness.Completions);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(turns, harness.PromptCount);

        // A transcript arriving means the provider is still transcribing, so it is told to stop.
        Assert.Equal(stops + 1, harness.Media.TranscriptionStops);

        // And the caller is seated once.
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ASilenceAfterTheHandoff_IsNotBrokenByTheAssistant()
    {
        // Arrange
        // Whichever service carried the handoff out, the loop's own record of it is what keeps the silence
        // watch from asking a caller who is on hold, or talking to an agent, whether they are still there.
        var harness = await HandedOffAsync(HandoffDisposition.Routed);
        var spoken = harness.Media.Spoken.Count;

        // Act
        await harness.Loop.OnListeningTimedOutAsync(
            new TurnBasedSilence
            {
                ActivityId = harness.Activity.ItemId,
                ProviderName = "Fake",
                ProviderCallId = "call-1",
                PromptCount = harness.PromptCount,
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(harness.Activity.AiEscalated);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
    }

    [Fact]
    public async Task ASecondTransferAfterTheHandoff_IsNotCarriedOut()
    {
        // Arrange
        // A turn that was already in flight when the handoff happened can still record a transfer of its own.
        // Carrying it out enqueued nothing new -- the Contact Center refuses a second seat -- but the assistant
        // announced "connecting you" again over the agent who was already on the line.
        var harness = await HandedOffAsync(HandoffDisposition.Routed);
        var spoken = harness.Media.Spoken.Count;
        var starts = harness.Media.TranscriptionStarts;

        harness.Activity.Put(new PendingVoiceHandoff { Reason = "the caller asked again" });

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(starts, harness.Media.TranscriptionStarts);
        Assert.False(harness.Activity.TryGet<PendingVoiceHandoff>(out _));
    }

    [Fact]
    public async Task AnAfterHoursHandoff_StillEndsTheCallOnceItsClosingLineIsHeard()
    {
        // Arrange
        // The one handoff that is followed by the assistant speaking: nobody is there to take the call, so it
        // says a callback is booked and hangs up. Being handed off must not swallow that hangup.
        var harness = await HandedOffAsync(HandoffDisposition.CallbackScheduled);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task AHandoffThatFails_IsNotRecordedAsHandedOff()
    {
        // Arrange
        // The fallback for a transfer with nowhere to go is to end the call, and a call that was never passed to
        // anybody must not be counted as one that was.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.HandoffResult = OmnichannelHandoffResult.Failure("No queue.");
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Activity.AiEscalated);
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task ARealtimeHandoffFinishedTwice_SeatsAndAnnouncesTheCallerOnce()
    {
        // Arrange
        // The realtime session ends at the handoff, but the work it hands on can still be delivered twice. The
        // second delivery must not announce the transfer over the agent who has taken the call.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();
        var spoken = harness.Media.Spoken.Count;
        await harness.FinishAsync();

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(0, harness.Media.TranscriptionStarts);
    }

    /// <summary>
    /// Runs a turn-based call up to the moment the caller has been handed over: the caller asks for a person, the
    /// assistant says its bridge line, and the end of that line performs the handoff and announces it.
    /// </summary>
    private static async Task<LoopHarness> HandedOffAsync(HandoffDisposition disposition)
    {
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.HandoffResult = disposition switch
        {
            HandoffDisposition.WaitingInQueue => OmnichannelHandoffResult.WaitingInQueue(),
            HandoffDisposition.CallbackScheduled => OmnichannelHandoffResult.CallbackScheduled(),
            _ => OmnichannelHandoffResult.Success(offeredToUserId: "agent-1"),
        };
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);

        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        return harness;
    }
}
