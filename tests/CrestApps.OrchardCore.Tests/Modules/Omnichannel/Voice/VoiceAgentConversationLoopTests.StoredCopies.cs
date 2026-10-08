using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The handoff as it reaches a real store: each webhook reads its own copy of the activity, so the only thing that
/// keeps the assistant out of the caller's conversation with the agent is what the handoff actually saved.
/// </summary>
/// <remarks>
/// Every other test here hands the loop the same activity object on every read, so a change made to any copy is seen
/// by every later webhook whether or not it was ever saved. That hid a missing write: the loop's record that the call
/// was handed over could be dropped from what it saves and nothing failed, because the in-memory copy it also marks
/// was the very object the next webhook read.
/// </remarks>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Theory]
    [MemberData(nameof(AcceptedHandoffs))]
    public async Task OnceHandedOff_TheNextWebhooksReadACallThatIsHandedOver_AndTheAssistantStaysSilent(HandoffDisposition disposition)
    {
        // Arrange
        // The handoff service is a stand-in that records nothing on the activity, so the loop's own write is the
        // whole of the record here, as it is for any handoff service that does not mark the call itself.
        var harness = await HandedOffWithStoredCopiesAsync(disposition);
        var starts = harness.Media.TranscriptionStarts;
        var watches = harness.SilenceWatchdog.Armed.Count;
        var spoken = harness.Media.Spoken.Count;
        var completions = harness.Completions;

        // Act
        // The announcement of the transfer finishes, then the caller is heard talking to the agent.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Hi, yes, I was waiting to be connected.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // Nothing listens, watches, answers or speaks again.
        Assert.Equal(starts, harness.Media.TranscriptionStarts);
        Assert.Equal(watches, harness.SilenceWatchdog.Armed.Count);
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(completions, harness.Completions);
        Assert.Equal(0, harness.Media.Hangups);
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Because the handoff was saved, not only remembered.
        var stored = harness.ReadStoredCopy();
        Assert.True(stored.AiEscalated);
        Assert.False(stored.TryGet<PendingVoiceHandoff>(out _));
    }

    [Fact]
    public async Task OnceHandedOff_ASilenceReadFromTheStore_IsNotBrokenByTheAssistant()
    {
        // Arrange
        var harness = await HandedOffWithStoredCopiesAsync(HandoffDisposition.WaitingInQueue);
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
        Assert.Equal(spoken, harness.Media.Spoken.Count);
        Assert.Equal(0, harness.Media.Hangups);
        Assert.True(harness.ReadStoredCopy().AiEscalated);
    }

    /// <summary>
    /// Runs a turn-based call up to the moment the caller has been handed over, against a store that hands every read
    /// its own copy of what was last saved.
    /// </summary>
    private static async Task<LoopHarness> HandedOffWithStoredCopiesAsync(HandoffDisposition disposition)
    {
        var harness = new LoopHarness(storesCopies: true);
        harness.EnableHandoff();
        harness.HandoffResult = disposition == HandoffDisposition.WaitingInQueue
            ? OmnichannelHandoffResult.WaitingInQueue()
            : OmnichannelHandoffResult.Success(offeredToUserId: "agent-1");
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

    private sealed partial class LoopHarness
    {
        private string _storedActivity;

        /// <summary>
        /// Reads the activity as a store would: a fresh copy of what was last saved, starting from
        /// <see cref="Activity"/> as seeded.
        /// </summary>
        public OmnichannelActivity ReadStoredCopy()
        {
            _storedActivity ??= JsonSerializer.Serialize(Activity, JOptions.Default);

            return JsonSerializer.Deserialize<OmnichannelActivity>(_storedActivity, JOptions.Default);
        }

        private void Store(OmnichannelActivity activity)
            => _storedActivity = JsonSerializer.Serialize(activity, JOptions.Default);
    }
}
