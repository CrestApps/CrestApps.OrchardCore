using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The customer leg an automated AI voice agent is talking to, before and after it is handed to a live agent.
/// </summary>
public sealed partial class TelnyxOutboundBridgeOrchestrator
{
    // The events that move a call through its lifecycle. Everything else on the leg (speech, transcription, playback)
    // belongs to the conversation alone and never changes the call's state.
    private static readonly HashSet<string> _handedOffLifecycleEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "call.answered",
        "call.bridged",
        "call.hangup",
    };

    /// <summary>
    /// Runs the conversation's handlers, and decides whether the leg's event also belongs to the Contact Center.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A leg an automated AI voice agent handles is driven by the conversation loop in the optional AI voice
    /// handlers (answered, transcription, speak-ended, hangup). Until the AI hands the caller over it is nobody else's
    /// business, so it is reported as a hidden internal leg (<see cref="TelnyxOutboundBridgeLeg.DestinationLeg"/>)
    /// that stays out of Contact Center normalization.
    /// </para>
    /// <para>
    /// After the handover it is the caller's leg of a Contact Center interaction, and it keeps the AI's client state
    /// for the rest of the call. Hiding its events then hid the only two that say what happened to the call: the
    /// bridge that joins the agent, so the call never became connected and recorded no answer and no talk time; and
    /// the hangup, so reconciliation ended it later as an unanswered call and the agent skipped wrap-up. Those
    /// lifecycle events now flow on to normalization exactly as a routed caller's do.
    /// </para>
    /// <para>
    /// Whether the call is the Contact Center's is decided before the handlers run: the conversation's own hangup
    /// handling releases a caller who was still waiting, which can end the interaction the question is about.
    /// </para>
    /// </remarks>
    private async Task<TelnyxOutboundBridgeLeg> AdvanceAiVoiceLegAsync(
        TelnyxCallEvent callEvent,
        TelnyxOutboundBridgeState state,
        CancellationToken cancellationToken)
    {
        var handedToContactCenter = await IsHandedToContactCenterAsync(callEvent, cancellationToken);

        foreach (var handler in _aiVoiceEventHandlers)
        {
            await handler.HandleAsync(callEvent, state, cancellationToken);
        }

        return handedToContactCenter
            ? TelnyxOutboundBridgeLeg.None
            : TelnyxOutboundBridgeLeg.DestinationLeg;
    }

    private async Task<bool> IsHandedToContactCenterAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken)
    {
        if (_interactionProbe is null ||
            string.IsNullOrWhiteSpace(callEvent.CallControlId) ||
            string.IsNullOrWhiteSpace(callEvent.EventType) ||
            !_handedOffLifecycleEvents.Contains(callEvent.EventType.Trim()))
        {
            return false;
        }

        return await _interactionProbe.HasActiveInteractionAsync(
            TelnyxConstants.ProviderTechnicalName,
            callEvent.CallControlId,
            cancellationToken);
    }
}
