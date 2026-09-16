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
        }, cancellationToken);
    }

    private static VoiceAgentEventKind ResolveKind(string eventType)
        => eventType?.Trim().ToLowerInvariant() switch
        {
            "call.answered" => VoiceAgentEventKind.Answered,
            "call.speak.ended" => VoiceAgentEventKind.SpeechEnded,
            "call.transcription" => VoiceAgentEventKind.Transcription,
            "call.hangup" => VoiceAgentEventKind.Hangup,
            _ => VoiceAgentEventKind.Unknown,
        };
}
