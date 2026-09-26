using System.Collections.Concurrent;
using CrestApps.OrchardCore.WebSockets.Services;

namespace CrestApps.OrchardCore.Tests.WebSockets.Doubles;

/// <summary>
/// The shared ownership store, in memory: what Redis does for the registry, without a server. Set
/// <see cref="Fail"/> to make every operation throw, which is how the tests reach the degraded path that keeps
/// call audio working when Redis is down.
/// </summary>
internal sealed class FakeRendezvousOwnerStore : IRendezvousOwnerStore
{
    private readonly ConcurrentDictionary<string, (string NodeId, TimeSpan TimeToLive)> _owners = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether every operation throws, standing in for an unreachable store.
    /// </summary>
    public bool Fail { get; set; }

    /// <summary>
    /// Returns the expiry recorded for a key, or <see cref="TimeSpan.Zero"/> when it is not held.
    /// </summary>
    public TimeSpan TimeToLive(string key)
        => _owners.TryGetValue(key, out var entry) ? entry.TimeToLive : TimeSpan.Zero;

    /// <inheritdoc/>
    public Task<bool> TryTakeOwnershipAsync(string key, string nodeId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();

        return Task.FromResult(_owners.TryAdd(key, (nodeId, timeToLive)));
    }

    /// <inheritdoc/>
    public Task<string> GetOwnerAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();

        return Task.FromResult(_owners.TryGetValue(key, out var entry) ? entry.NodeId : null);
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();

        _owners.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (Fail)
        {
            throw new InvalidOperationException("The shared rendezvous store is unreachable.");
        }
    }
}
