namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// The outcome of writing a real-time provider payload into the bounded ingestion buffer.
/// </summary>
internal enum AsteriskRealtimeIngestionWriteResult
{
    /// <summary>
    /// The payload was buffered, either immediately or after absorbing backpressure within the bounded wait.
    /// </summary>
    Written,

    /// <summary>
    /// The buffer stayed full for the whole backpressure window, so the caller should reconnect and reconcile
    /// provider state rather than block the receive loop indefinitely.
    /// </summary>
    BackpressureTimedOut,
}
