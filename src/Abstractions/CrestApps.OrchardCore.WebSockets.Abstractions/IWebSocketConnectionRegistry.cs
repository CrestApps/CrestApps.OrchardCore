namespace CrestApps.OrchardCore.WebSockets;

/// <summary>
/// Correlates a provider-initiated WebSocket callback with the request that started it. A feature that asks a
/// provider to dial back to a hosted WebSocket registers a rendezvous under an unguessable key, embeds that key in
/// the callback URL, and awaits <see cref="WebSocketRendezvous.ConnectedTask"/>; the hosting endpoint claims the
/// rendezvous by key when the socket arrives and hands the socket over.
/// </summary>
/// <remarks>
/// The methods are asynchronous so an implementation can be backed by an external store. The default implementation
/// is a per-node in-memory registry and completes synchronously; a distributed implementation (for example one
/// gated on the Redis feature) may perform network I/O to record and look up the owning node. Note that a live
/// socket cannot be moved across nodes, so a distributed registry supports discovery and affinity rather than
/// transparent cross-node socket hand-off, which requires a relay on top.
/// </remarks>
public interface IWebSocketConnectionRegistry
{
    /// <summary>
    /// Registers a pending rendezvous under the supplied key and returns it. The key must be unique; a duplicate
    /// key throws.
    /// </summary>
    /// <param name="key">The unguessable correlation key embedded in the callback URL.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rendezvous the starter awaits.</returns>
    Task<WebSocketRendezvous> RegisterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims and removes the pending rendezvous for the supplied key. Returns <see langword="null"/>
    /// when no rendezvous is pending for the key (unknown, already claimed, expired, or registered on another node).
    /// </summary>
    /// <param name="key">The correlation key presented on the WebSocket request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The claimed rendezvous, or <see langword="null"/> when none is pending.</returns>
    Task<WebSocketRendezvous> TryClaimAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a pending rendezvous for the supplied key if it is still registered. Used by the starter to clean up
    /// after a timeout or failure so an abandoned key cannot be claimed later.
    /// </summary>
    /// <param name="key">The correlation key to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
