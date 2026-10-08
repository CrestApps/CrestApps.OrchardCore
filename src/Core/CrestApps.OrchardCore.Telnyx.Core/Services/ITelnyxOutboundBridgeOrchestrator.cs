namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Identifies which leg of an outbound soft-phone bridge a webhook event belongs to, so the webhook pipeline
/// can decide whether the event should surface to the soft phone.
/// </summary>
public enum TelnyxOutboundBridgeLeg
{
    /// <summary>The event does not belong to an outbound-bridge leg the platform created.</summary>
    None,

    /// <summary>The event belongs to the agent leg — the call the soft phone tracks and displays.</summary>
    AgentLeg,

    /// <summary>The event belongs to the internal destination leg the platform dials and bridges.</summary>
    DestinationLeg,
}

/// <summary>
/// Advances the two-leg outbound bridge that connects an agent's browser soft phone to a dialed destination.
/// When an agent places an outbound call, the platform first rings the agent's browser endpoint; once that
/// leg answers this orchestrator dials the destination, and once the destination answers it bridges the two.
/// </summary>
public interface ITelnyxOutboundBridgeOrchestrator
{
    /// <summary>
    /// Inspects a call event and, when it belongs to an outbound bridge, issues the next Telnyx command
    /// (dial the destination when the agent answered; bridge the legs when the destination answered).
    /// </summary>
    /// <param name="callEvent">The parsed Telnyx call event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Which bridge leg the event belonged to, if any.</returns>
    Task<TelnyxOutboundBridgeLeg> AdvanceAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default);
}
