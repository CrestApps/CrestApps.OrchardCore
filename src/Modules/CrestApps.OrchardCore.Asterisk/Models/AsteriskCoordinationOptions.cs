namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// The Asterisk timings a deployment tunes rather than the product fixing. The credential lock protects the
/// PJSIP realtime table from concurrent issuance for the same endpoint, and the reclamation threshold decides
/// how long an inbound call may sit unclaimed before another node takes it over.
/// </summary>
public sealed class AsteriskCoordinationOptions
{
    /// <summary>
    /// Gets or sets how long a caller waits to acquire the PJSIP credential issuance lock before giving up.
    /// </summary>
    public TimeSpan CredentialLockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long the PJSIP credential issuance lock is held before it expires on its own, which
    /// bounds how long a crashed node blocks issuance for the same endpoint.
    /// </summary>
    public TimeSpan CredentialLockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long an inbound call may remain pending before reconciliation reclaims it. Setting this
    /// below the longest expected routing delay causes a call to be reclaimed while it is still being answered.
    /// </summary>
    public TimeSpan PendingReclamationThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the ceiling on a single ARI request including retries.
    /// </summary>
    public TimeSpan HttpTotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the ceiling on one ARI request attempt.
    /// </summary>
    public TimeSpan HttpAttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how many real-time provider events may be buffered between the WebSocket receive loop and the
    /// dispatcher before the buffer is full and backpressure is applied. A larger buffer absorbs longer dispatch
    /// stalls at the cost of memory; it does not make the stream lossless. The value is validated to stay within a
    /// positive, memory-safe range.
    /// </summary>
    public int RealtimeEventBufferCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets how long the real-time receive loop stops draining the socket while the buffer is saturated
    /// before it gives up and reconnects to reconcile provider state. While the loop is not reading, TCP flow
    /// control slows the provider only for as long as the provider tolerates a stalled reader: Asterisk closes the
    /// ARI WebSocket and discards its queued events once its own <c>websocket_write_timeout</c> elapses (100 ms by
    /// default), so this window only reaches the provider when that timeout is set to exceed it. Otherwise the
    /// provider tears the socket down first and the listener degrades to reconnect and reconcile.
    /// </summary>
    public TimeSpan RealtimeEventBackpressureTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
