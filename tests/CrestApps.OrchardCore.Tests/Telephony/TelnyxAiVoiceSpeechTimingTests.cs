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
    [Theory]
    [InlineData("call.speak.started", VoiceAgentEventKind.SpeechStarted)]
    [InlineData("call.speak.ended", VoiceAgentEventKind.SpeechEnded)]
    public async Task TheAssistantsSpeech_ReachesTheLoop_WithWhenTheProviderSaysItHappened(string eventType, VoiceAgentEventKind expectedKind)
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
    }
}
