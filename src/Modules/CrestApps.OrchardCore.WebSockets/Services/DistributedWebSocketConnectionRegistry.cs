using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.WebSockets.Services;

/// <summary>
/// A rendezvous registry for a multi-node deployment. The rendezvous itself stays on the node that created it —
/// a live WebSocket cannot be moved between processes — and a shared store records which node that is.
/// <para>
/// That record is the whole point. Without it, a callback that lands on the wrong node gets "no rendezvous
/// pending", which is exactly what an unknown key, an expired key, and a key held by another node all look like;
/// the call loses its audio and nothing in the log says why. With it, the wrong node still cannot serve the
/// socket, but it knows that is what happened, names the node that can, and leaves the rendezvous intact for the
/// provider's retry.
/// </para>
/// </summary>
internal sealed class DistributedWebSocketConnectionRegistry : IWebSocketConnectionRegistry
{
    /// <summary>
    /// How long a shared ownership record survives on its own. Long enough for a provider to dial back and for a
    /// media session to be set up; short enough that a node which dies mid-call does not hold the key until
    /// somebody notices.
    /// </summary>
    private static readonly TimeSpan _ownershipTimeToLive = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, WebSocketRendezvous> _pending = new(StringComparer.Ordinal);
    private readonly IRendezvousOwnerStore _owners;
    private readonly string _nodeId;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedWebSocketConnectionRegistry"/> class.
    /// </summary>
    /// <param name="owners">The shared record of which node holds which key.</param>
    /// <param name="nodeId">This node's identity.</param>
    /// <param name="logger">The logger.</param>
    public DistributedWebSocketConnectionRegistry(
        IRendezvousOwnerStore owners,
        string nodeId,
        ILogger<DistributedWebSocketConnectionRegistry> logger)
    {
        _owners = owners;
        _nodeId = nodeId;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<WebSocketRendezvous> RegisterAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var rendezvous = new WebSocketRendezvous();

        if (!_pending.TryAdd(key, rendezvous))
        {
            throw new InvalidOperationException("A WebSocket rendezvous is already pending for this key.");
        }

        bool owned;

        try
        {
            owned = await _owners.TryTakeOwnershipAsync(key, _nodeId, _ownershipTimeToLive, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The shared store is down. Degrading to per-node behaviour keeps the call working whenever the
            // callback returns to this node, which is the common case; failing here would take call audio down
            // with Redis.
            _logger.LogWarning(ex, "The shared WebSocket rendezvous store is unreachable; falling back to node-local rendezvous only.");

            return rendezvous;
        }

        if (!owned)
        {
            // The key is unguessable, so two nodes holding it is a bug rather than a collision — and it would
            // bind the provider's callback to whichever won the race, leaving the other call silent.
            _pending.TryRemove(key, out _);

            throw new InvalidOperationException("A WebSocket rendezvous is already pending for this key on another node.");
        }

        return rendezvous;
    }

    /// <inheritdoc/>
    public async Task<WebSocketRendezvous> TryClaimAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (_pending.TryRemove(key, out var rendezvous))
        {
            await ReleaseQuietlyAsync(key, cancellationToken);

            return rendezvous;
        }

        // Not ours. Say which node has it, so a misrouted callback reads as a routing problem rather than as a
        // key nobody has ever seen. The rendezvous is left alone: the provider's retry may well reach the node
        // that owns it, and tearing the ownership down here would make one stray callback end the call.
        string owner = null;

        try
        {
            owner = await _owners.GetOwnerAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The shared WebSocket rendezvous store is unreachable while claiming a rendezvous.");
        }

        if (!string.IsNullOrEmpty(owner) && !string.Equals(owner, _nodeId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "A WebSocket callback arrived on node '{NodeId}' for a rendezvous held by node '{OwnerNodeId}'. A live socket cannot be handed between nodes; route provider callbacks to the node that started them.",
                _nodeId,
                owner);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_pending.TryRemove(key, out _))
        {
            await ReleaseQuietlyAsync(key, cancellationToken);
        }
    }

    private async Task ReleaseQuietlyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _owners.ReleaseAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The record expires on its own, so a failed release costs nothing but a stale name for a few minutes.
            _logger.LogWarning(ex, "Failed to release a WebSocket rendezvous from the shared store; it will expire on its own.");
        }
    }
}
