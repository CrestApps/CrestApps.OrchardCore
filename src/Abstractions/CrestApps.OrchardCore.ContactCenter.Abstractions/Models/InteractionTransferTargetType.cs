namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the kind of destination a live interaction is transferred to.
/// </summary>
public enum InteractionTransferTargetType
{
    /// <summary>
    /// The interaction is transferred to a specific agent.
    /// </summary>
    Agent,

    /// <summary>
    /// The interaction is transferred to a queue for re-routing.
    /// </summary>
    Queue,

    /// <summary>
    /// The interaction is transferred to an external destination (for example an external phone number).
    /// </summary>
    External,

    /// <summary>
    /// The interaction is transferred to an inbound entry point (for example an IVR or announcement).
    /// </summary>
    EntryPoint,
}
