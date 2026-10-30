using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Warm and screened transfers of an AI call: the number rings on a side call while the caller holds, whoever answers
/// is briefed or asked, and the two are connected or the caller goes back to the AI.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task AWarmTransfer_BriefsWhoeverAnswers_ThenConnectsThem()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "A caller needs help with a refund.");

        // Act
        var started = await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.Answered);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.SpeechEnded);

        // Assert
        Assert.True(started);
        Assert.Equal("+15552223333", Assert.Single(harness.Media.SideCalls).PhoneNumber);
        Assert.Contains("A caller needs help with a refund.", harness.Media.Spoken);
        Assert.Equal(("call-1", "side-1"), Assert.Single(harness.Media.Bridges));
        Assert.True(harness.Activity.AiEscalated);
        Assert.False(harness.Activity.Has<AIAttendedTransfer>());
        Assert.Equal(AICallTransferMode.Warm, Part<AICallTransfer>(harness.Activity).Mode);
    }

    [Fact]
    public async Task AScreenedTransfer_ThatIsDeclined_GivesTheCallerBackToTheAI()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Screened, briefing: "A caller about an invoice.");
        harness.Reply = "They can't take the call right now. Shall I have them call you back?";

        // Act
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.Answered);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.DigitsReceived, digits: "2");

        // Assert
        Assert.Contains(harness.Media.Gathered, text => text.Contains("A caller about an invoice.", StringComparison.Ordinal) && text.Contains("Press 1", StringComparison.Ordinal));
        Assert.Empty(harness.Media.Bridges);
        Assert.Contains("side-1", harness.Media.HungUpCalls);
        Assert.Contains(harness.Reply, harness.Media.Spoken);
        Assert.Contains(harness.Transcript, message => message.Text.Contains("they could not take the call", StringComparison.Ordinal));
        Assert.False(harness.Activity.AiEscalated);
        Assert.False(harness.Activity.Has<AIAttendedTransfer>());
    }

    [Fact]
    public async Task AScreenedTransfer_ThatIsAccepted_ConnectsTheCaller()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Screened, briefing: "A caller about an invoice.");

        // Act
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.Answered);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.DigitsReceived, digits: "1");

        // Assert
        Assert.Single(harness.Media.Bridges);
        Assert.True(harness.Activity.AiEscalated);
    }

    [Fact]
    public async Task ATransferNobodyAnswers_GivesTheCallerBackToTheAIWithTheReason()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "Brief.", holdAudio: "https://example.com/hold.mp3");

        // Act
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.Hangup);

        // Assert
        Assert.Equal(["https://example.com/hold.mp3"], harness.Media.PlayedAudio);
        Assert.Equal(1, harness.Media.AudioStops);
        Assert.Contains(harness.Transcript, message => message.Text.Contains("nobody answered", StringComparison.Ordinal));
        Assert.False(harness.Activity.Has<AIAttendedTransfer>());
    }

    [Fact]
    public async Task ATransferThatReachesAVoicemail_IsNotConnected()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "Brief.");

        // Act
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.Answered);
        await HandleSideCallAsync(harness, VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine);

        // Assert
        Assert.Empty(harness.Media.Bridges);
        Assert.Contains("side-1", harness.Media.HungUpCalls);
        Assert.Contains(harness.Transcript, message => message.Text.Contains("voicemail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACallerWhoHangsUpWhileHolding_HangsUpTheSideCallToo()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "Brief.");
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("side-1", harness.Media.HungUpCalls);
        Assert.False(harness.Activity.Has<AIAttendedTransfer>());
    }

    [Fact]
    public async Task TheEndOfThePleaseHoldLine_DoesNotStartListening()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "Brief.");
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);
        var startsBefore = harness.Media.TranscriptionStarts;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Please hold while I connect you.", harness.Media.Spoken);
        Assert.Equal(startsBefore, harness.Media.TranscriptionStarts);
    }

    [Fact]
    public async Task AProviderThatCannotPlaceASideCall_TransfersTheCallerCold()
    {
        // Arrange
        var harness = StartTransferHarness(AICallTransferMode.Warm, briefing: "Brief.");
        harness.Media.CanPlaceSideCalls = false;

        // Act
        await harness.Loop.TakeOverTurnBasedCallAsync(harness.Activity, Part<AILiveCall>(harness.Activity), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["+15552223333"], harness.Media.Transfers);
        Assert.Equal(AICallTransferMode.Cold, Part<AICallTransfer>(harness.Activity).Mode);
    }

    [Fact]
    public void TheTransferFailedTurn_TellsTheAIWhatHappened()
        => Assert.Contains("the line was busy", VoiceAgentConversationLoop.TransferFailedGuidance("the line was busy"));

    private static T Part<T>(OmnichannelActivity activity)
        where T : class, new()
        => activity.TryGet<T>(out var part) ? part : default;

    private static LoopHarness StartTransferHarness(AICallTransferMode mode, string briefing, string holdAudio = null)
    {
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.InProgress;
        harness.Activity.InteractionType = ActivityInteractionType.Automated;
        harness.Activity.AISessionId = "session-1";
        harness.Activity.Put(new AILiveCall { ProviderName = "Fake", ProviderCallId = "call-1" });
        harness.Activity.Put(new AITakeOverRequest
        {
            TargetPhoneNumber = "+15552223333",
            TransferMode = mode,
            Briefing = briefing,
            HoldAudio = holdAudio,
        });

        return harness;
    }

    private static Task HandleSideCallAsync(
        LoopHarness harness,
        VoiceAgentEventKind kind,
        string digits = null,
        VoiceAgentAnswerer answerer = VoiceAgentAnswerer.Unknown)
        => harness.Loop.HandleAsync(
            new VoiceAgentEvent
            {
                Kind = kind,
                ProviderCallId = "side-1",
                ProviderName = "Fake",
                ActivityId = harness.Activity.ItemId,
                IsSideCall = true,
                PeerProviderCallId = "call-1",
                Digits = digits,
                Answerer = answerer,
            },
            TestContext.Current.CancellationToken);
}
