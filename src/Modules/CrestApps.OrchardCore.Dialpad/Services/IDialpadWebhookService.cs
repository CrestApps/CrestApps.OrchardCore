namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Handles a parsed Dialpad call event: it projects provider call-state changes onto the shared Telephony
/// stream and routes new inbound calls when a higher-level voice feature is available.
/// </summary>
public interface IDialpadWebhookService
{
    /// <summary>
    /// Processes a Dialpad call event.
    /// </summary>
    /// <param name="callEvent">The parsed Dialpad call event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result describing how the event was handled.</returns>
    Task<DialpadWebhookResult> ProcessAsync(DialpadCallEvent callEvent, CancellationToken cancellationToken = default);
}
