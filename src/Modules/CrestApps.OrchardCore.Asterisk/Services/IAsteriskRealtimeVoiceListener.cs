namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Owns the single Asterisk ARI WebSocket subscription for a tenant shell. The listener is started only
/// after the caller has completed provider-local validation and acquired ARI-application ownership, so the
/// WebSocket is never opened for an unconfigured provider or an application owned by another tenant.
/// </summary>
internal interface IAsteriskRealtimeVoiceListener
{
    /// <summary>
    /// Opens the ARI WebSocket and begins dispatching real-time voice events for the supplied resolved
    /// endpoints. Calling this while a listener is already running is a no-op.
    /// </summary>
    /// <param name="listeners">The resolved Asterisk endpoints to subscribe to.</param>
    /// <returns>A task that completes once the listener has been scheduled to start.</returns>
    Task StartAsync(IReadOnlyList<AsteriskResolvedSettings> listeners);

    /// <summary>
    /// Stops the running listener, closing the ARI WebSocket and cancelling in-flight dispatch.
    /// </summary>
    /// <returns>A task that completes once the listener has stopped.</returns>
    Task StopAsync();
}
