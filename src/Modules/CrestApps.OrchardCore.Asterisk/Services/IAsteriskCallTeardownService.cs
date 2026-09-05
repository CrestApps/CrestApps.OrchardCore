namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Releases Asterisk call resources when a channel reaches a terminal state. It is invoked only for events that
/// report the channel has ended, independently of the bridge pipeline, because releasing ARI bridges, channels,
/// and ownership bindings is orthogonal to projecting call status and must happen whether or not a bridge claimed
/// the event. The dispatcher applies the terminal test before the fan-out, so an implementation is never asked to
/// release resources for the stream of non-terminal events a live channel produces.
/// </summary>
internal interface IAsteriskCallTeardownService
{
    /// <summary>
    /// Releases the resources associated with a channel that reached a terminal state. Channels not owned by the
    /// current tenant are ignored. The caller only invokes this for terminal events, but an implementation is
    /// still expected to tolerate a non-terminal event rather than act on it.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when any owned terminal resources have been released.</returns>
    Task ReleaseAsync(AsteriskRealtimeVoiceEvent voiceEvent, CancellationToken cancellationToken = default);
}
