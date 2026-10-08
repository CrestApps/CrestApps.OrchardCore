using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A turn-based call's talk time comes from the provider telling the loop when the assistant's speech started and
/// ended, measured on the time the provider says each happened rather than when its webhook arrived.
/// </summary>
public sealed class TelnyxAiVoiceSpeechTimingTests
{
    // The speech events time the assistant's talk; answered starts the conversation, transcription carries what the caller
    // said and hangup concludes the call. Confirmed live end to end on an automated AI voice call.
    [Theory]
    [InlineData("call.speak.started", VoiceAgentEventKind.SpeechStarted)]
    [InlineData("call.speak.ended", VoiceAgentEventKind.SpeechEnded)]
    [InlineData("call.answered", VoiceAgentEventKind.Answered)]
    [InlineData("call.transcription", VoiceAgentEventKind.Transcription)]
    [InlineData("call.hangup", VoiceAgentEventKind.Hangup)]
    public async Task ACallEvent_ReachesTheLoop_AsItsKind_WithWhenTheProviderSaysItHappened(string eventType, VoiceAgentEventKind expectedKind)
    {
        // Arrange
        VoiceAgentEvent handled = null;
        var loop = new Mock<IVoiceAgentConversationLoop>();
        loop.Setup(x => x.HandleAsync(It.IsAny<VoiceAgentEvent>(), It.IsAny<CancellationToken>()))
            .Callback<VoiceAgentEvent, CancellationToken>((voiceEvent, _) => handled = voiceEvent)
            .Returns(Task.CompletedTask);
        var handler = new TelnyxAiVoiceConversationHandler(loop.Object);
        var occurred = new DateTime(2026, 9, 24, 15, 0, 1, 250, DateTimeKind.Utc);

        // Act
        await handler.HandleAsync(
            new TelnyxCallEvent { EventType = eventType, CallControlId = "ctrl-1", OccurredUtc = occurred },
            new TelnyxOutboundBridgeState { Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent, ActivityId = "activity-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedKind, handled.Kind);
        Assert.Equal(occurred, handled.OccurredUtc);
        Assert.Equal("ctrl-1", handled.ProviderCallId);
        Assert.Equal("activity-1", handled.ActivityId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ATranscription_ReachesTheLoop_WithWhatTheCallerSaid_AndWhetherItIsFinal(bool isFinal)
    {
        // Arrange
        // The loop answers only a final transcription; a partial one it answered would talk over the caller mid-sentence.
        VoiceAgentEvent handled = null;
        var loop = new Mock<IVoiceAgentConversationLoop>();
        loop.Setup(x => x.HandleAsync(It.IsAny<VoiceAgentEvent>(), It.IsAny<CancellationToken>()))
            .Callback<VoiceAgentEvent, CancellationToken>((voiceEvent, _) => handled = voiceEvent)
            .Returns(Task.CompletedTask);
        var handler = new TelnyxAiVoiceConversationHandler(loop.Object);

        // Act
        await handler.HandleAsync(
            new TelnyxCallEvent
            {
                EventType = "call.transcription",
                CallControlId = "ctrl-1",
                TranscriptionText = "I would like to reschedule.",
                TranscriptionIsFinal = isFinal,
            },
            new TelnyxOutboundBridgeState { Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent, ActivityId = "activity-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceAgentEventKind.Transcription, handled.Kind);
        Assert.Equal("I would like to reschedule.", handled.TranscriptionText);
        Assert.Equal(isFinal, handled.TranscriptionIsFinal);
    }
}
