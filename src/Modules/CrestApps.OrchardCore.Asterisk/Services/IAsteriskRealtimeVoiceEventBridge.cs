namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Intercepts an Asterisk real-time voice event that represents internal call-control orchestration rather
/// than a call-state change worth projecting.
/// <para>
/// A bridge that returns <see langword="true"/> <em>absorbs</em> the event: it is removed from the stream and
/// never reaches the normalized ingestion path. Only orchestration concerns may do that — answering and
/// parking a first-seen inbound channel, or releasing a module-originated agent leg. A consumer that merely
/// wants to observe call state must implement
/// <see cref="CrestApps.OrchardCore.Telephony.Core.Services.INormalizedVoiceEventHandler"/> instead, because
/// absorbing the event there would silently desynchronize every other projection of the same call.
/// </para>
/// </summary>
internal interface IAsteriskRealtimeVoiceEventBridge
{
    /// <summary>
    /// Attempts to absorb the specified event as internal call-control orchestration.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the event was absorbed; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default);
}
