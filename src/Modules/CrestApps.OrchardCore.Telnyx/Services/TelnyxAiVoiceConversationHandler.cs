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

        var notInService = TelnyxNotInServiceCauses.IsNotInService(callEvent);

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

            // A number the network says is not in service is completed without the assistant: there was never
            // anybody to talk to, and nothing for a review to read.
            HangupCause = notInService
                ? Telephony.Models.HangupCause.NotInService
                : IsBusy(callEvent) ? Telephony.Models.HangupCause.Busy : null,
            HangupDetail = TelnyxNotInServiceCauses.Describe(callEvent.HangupCause, callEvent.SipHangupCause),
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

    // The called party's line was busy: Telnyx's own cause, or the carrier's SIP 486 Busy Here / 600 Busy Everywhere.
    private static bool IsBusy(TelnyxCallEvent callEvent)
        => string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase) &&
            (callEvent.HangupCause?.Trim().ToLowerInvariant() is "user_busy" or "busy" ||
                callEvent.SipHangupCause?.Trim() is "486" or "600");

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
