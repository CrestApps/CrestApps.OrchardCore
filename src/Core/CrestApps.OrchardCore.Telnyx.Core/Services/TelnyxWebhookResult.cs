namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Describes how a Telnyx call-event webhook was handled.
/// </summary>
public enum TelnyxWebhookResult
{
    /// <summary>
    /// The event carried nothing actionable and was ignored.
    /// </summary>
    Ignored,

    /// <summary>
    /// The event updated an existing tracked call.
    /// </summary>
    Updated,

    /// <summary>
    /// The event created and routed a new inbound call.
    /// </summary>
    Routed,
}
