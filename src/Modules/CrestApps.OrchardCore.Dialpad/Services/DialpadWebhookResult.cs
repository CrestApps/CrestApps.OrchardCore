namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Identifies how a Dialpad call-event webhook was handled.
/// </summary>
public enum DialpadWebhookResult
{
    /// <summary>
    /// The event updated an existing Telephony or Contact Center projection.
    /// </summary>
    Updated,

    /// <summary>
    /// The event started a new inbound interaction and routed it through a higher-level voice feature.
    /// </summary>
    Routed,

    /// <summary>
    /// The event was ignored (unknown state, or no matching interaction for a non-inbound event).
    /// </summary>
    Ignored,
}
