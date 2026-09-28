using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.WebSockets.Services;

/// <summary>
/// The default per-node in-memory <see cref="IWebSocketConnectionRegistry"/>. Every operation completes
/// synchronously. Correct for a single-node deployment (or one that pins each provider callback to the originating
/// node with host-level affinity). A multi-node deployment behind a callback-agnostic load balancer needs a
/// distributed implementation instead.
/// </summary>
internal sealed class InMemoryWebSocketConnectionRegistry : IWebSocketConnectionRegistry
{
    private readonly ConcurrentDictionary<string, WebSocketRendezvous> _pending =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<WebSocketRendezvous> RegisterAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var rendezvous = new WebSocketRendezvous();

        if (!_pending.TryAdd(key, rendezvous))
        {
            throw new InvalidOperationException("A WebSocket rendezvous is already pending for this key.");
        }

        return Task.FromResult(rendezvous);
    }

    /// <inheritdoc/>
    public Task<WebSocketRendezvous> TryClaimAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<WebSocketRendezvous>(null);
        }

        _pending.TryRemove(key, out var rendezvous);

        return Task.FromResult(rendezvous);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _pending.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
