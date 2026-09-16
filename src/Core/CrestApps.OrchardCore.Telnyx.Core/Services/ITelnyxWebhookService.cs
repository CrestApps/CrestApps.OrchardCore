namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Processes normalized Telnyx call events: projects them through the shared Telephony voice-event ingress
/// and routes new inbound calls when a higher-level voice feature is available.
/// </summary>
public interface ITelnyxWebhookService
{
    /// <summary>
    /// Processes a parsed Telnyx call event.
    /// </summary>
    /// <param name="callEvent">The parsed Telnyx call event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The processing result.</returns>
    Task<TelnyxWebhookResult> ProcessAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default);
}
