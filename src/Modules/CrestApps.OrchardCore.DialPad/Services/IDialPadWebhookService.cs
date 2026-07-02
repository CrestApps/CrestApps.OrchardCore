namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Handles a parsed DialPad call event: it updates existing Contact Center interactions and routes new
/// inbound calls into the Contact Center.
/// </summary>
public interface IDialPadWebhookService
{
    /// <summary>
    /// Processes a DialPad call event.
    /// </summary>
    /// <param name="callEvent">The parsed DialPad call event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result describing how the event was handled.</returns>
    Task<DialPadWebhookResult> ProcessAsync(DialPadCallEvent callEvent, CancellationToken cancellationToken = default);
}
