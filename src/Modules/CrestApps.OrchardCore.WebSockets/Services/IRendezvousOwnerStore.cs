namespace CrestApps.OrchardCore.WebSockets.Services;

/// <summary>
/// Records which node holds the rendezvous for a correlation key, so a provider callback that lands on another
/// node can be answered deterministically instead of looking identical to an unknown or expired key.
/// </summary>
/// <remarks>
/// Deliberately separate from the registry: the registry owns the affinity policy — what to do when the socket
/// arrives somewhere else — and this owns nothing but the shared record of who holds what.
/// </remarks>
internal interface IRendezvousOwnerStore
{
    /// <summary>
    /// Records this node as the owner of the key, if nobody else already is.
    /// </summary>
    /// <param name="key">The correlation key.</param>
    /// <param name="nodeId">The node claiming it.</param>
    /// <param name="timeToLive">How long the record survives without being released, so a node that dies
    /// mid-call does not hold the key forever.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when this node now owns the key.</returns>
    Task<bool> TryTakeOwnershipAsync(string key, string nodeId, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the node holding the key, or <see langword="null"/> when nobody does.
    /// </summary>
    /// <param name="key">The correlation key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<string> GetOwnerAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the key.
    /// </summary>
    /// <param name="key">The correlation key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
