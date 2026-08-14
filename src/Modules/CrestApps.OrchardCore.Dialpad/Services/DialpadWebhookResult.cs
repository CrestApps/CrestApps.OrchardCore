namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Identifies how a Dialpad call-event webhook was handled by the Contact Center.
/// </summary>
public enum DialpadWebhookResult
{
    /// <summary>
    /// The event updated an existing interaction and call session.
    /// </summary>
    Updated,

    /// <summary>
    /// The event started a new inbound interaction and routed it to an agent.
    /// </summary>
    Routed,

    /// <summary>
    /// The event was ignored (unknown state, or no matching interaction for a non-inbound event).
    /// </summary>
    Ignored,
}
