namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Handles a parsed Dialpad call event: it updates existing Contact Center interactions and routes new
/// inbound calls into the Contact Center.
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
