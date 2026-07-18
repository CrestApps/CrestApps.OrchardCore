namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Releases Asterisk call resources when a channel reaches a terminal state. It is invoked for every real-time
/// voice event independently of the bridge pipeline, because releasing ARI bridges, channels, and ownership
/// bindings is orthogonal to projecting call status and must happen whether or not a bridge claimed the event.
/// </summary>
internal interface IAsteriskCallTeardownService
{
    /// <summary>
    /// Releases the resources associated with a channel that reached a terminal state. Non-terminal events and
    /// channels not owned by the current tenant are ignored.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when any owned terminal resources have been released.</returns>
    Task ReleaseAsync(AsteriskRealtimeVoiceEvent voiceEvent, CancellationToken cancellationToken = default);
}
