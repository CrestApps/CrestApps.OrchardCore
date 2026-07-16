namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Bridges an Asterisk real-time voice event into an optional higher-level call owner.
/// </summary>
internal interface IAsteriskRealtimeVoiceEventBridge
{
    /// <summary>
    /// Attempts to handle the specified event.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the event was handled; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default);
}
