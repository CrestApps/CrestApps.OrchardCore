using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Translates Telnyx call events into the terms the automated voice conversation is written in, and hands them
/// to the provider-neutral loop.
/// <para>
/// This is all that is Telnyx-specific about an automated voice call: the conversation itself — greeting,
/// listening, completing, escalating, concluding — lives in the Automated Voice module, so a second provider
/// adds automated voice with an adapter this size rather than a copy of the conversation.
/// </para>
/// </summary>
public sealed class TelnyxAiVoiceConversationHandler : ITelnyxAiVoiceEventHandler
{
    private readonly IVoiceAgentConversationLoop _loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxAiVoiceConversationHandler"/> class.
    /// </summary>
    /// <param name="loop">The provider-neutral conversation loop.</param>
    public TelnyxAiVoiceConversationHandler(IVoiceAgentConversationLoop loop)
    {
        _loop = loop;
    }

    /// <inheritdoc/>
    public Task HandleAsync(TelnyxCallEvent callEvent, TelnyxOutboundBridgeState state, CancellationToken cancellationToken = default)
    {
        if (callEvent is null || state is null)
        {
            return Task.CompletedTask;
        }

        return _loop.HandleAsync(new VoiceAgentEvent
        {
            Kind = ResolveKind(callEvent.EventType),
            ProviderCallId = callEvent.CallControlId,
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ActivityId = state.ActivityId,
            TranscriptionText = callEvent.TranscriptionText,
            TranscriptionIsFinal = callEvent.TranscriptionIsFinal,
            Answerer = ResolveAnswerer(callEvent.MachineDetectionResult),

            // When Telnyx says it happened, so the time between two events is not the time between two webhooks.
            OccurredUtc = callEvent.OccurredUtc,
        }, cancellationToken);
    }

    // Premium detection tells residences from businesses, and both are people. A fax is not somebody to talk to
    // any more than a voicemail is. Silence and "not sure" say nothing, so the call goes on as a conversation and
    // the greeting, if there is one, is still recognised from what it says.
    private static VoiceAgentAnswerer ResolveAnswerer(string result)
        => result?.Trim().ToLowerInvariant() switch
        {
            "human" or "human_residence" or "human_business" => VoiceAgentAnswerer.Person,
            "machine" or "fax_detected" => VoiceAgentAnswerer.Machine,
            _ => VoiceAgentAnswerer.Unknown,
        };

    private static VoiceAgentEventKind ResolveKind(string eventType)
        => eventType?.Trim().ToLowerInvariant() switch
        {
            "call.answered" => VoiceAgentEventKind.Answered,
            "call.speak.started" => VoiceAgentEventKind.SpeechStarted,
            "call.speak.ended" => VoiceAgentEventKind.SpeechEnded,
            "call.transcription" => VoiceAgentEventKind.Transcription,
            "call.hangup" => VoiceAgentEventKind.Hangup,
            "call.machine.detection.ended" or "call.machine.premium.detection.ended" => VoiceAgentEventKind.AnswererDetected,
            "call.machine.greeting.ended" or "call.machine.premium.greeting.ended" => VoiceAgentEventKind.MachineGreetingEnded,
            _ => VoiceAgentEventKind.Unknown,
        };
}
