namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Handles a Telnyx <c>call.recording.saved</c> webhook event. The base webhook pipeline invokes every
/// registered handler so higher-level features can react to a finished recording without the base feature
/// taking a dependency on them; when no handler is registered (for example, Contact Center Voice is not
/// enabled) a saved-recording event is simply ignored.
/// </summary>
public interface ITelnyxRecordingSavedHandler
{
    /// <summary>
    /// Handles a finished Telnyx recording.
    /// </summary>
    /// <param name="callEvent">The parsed Telnyx recording-saved event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the handler acted on the recording; otherwise, <see langword="false"/>.</returns>
    Task<bool> HandleAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default);
}
