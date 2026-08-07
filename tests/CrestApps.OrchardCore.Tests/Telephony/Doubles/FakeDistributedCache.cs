using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A deterministic in-memory <see cref="IDistributedCache"/> that stores entries without wall-clock
/// expiration, so cache-backed logic can be verified against a mocked <see cref="OrchardCore.Modules.IClock"/>
/// instead of real time. This keeps tests hermetic regardless of the host clock.
/// </summary>
internal sealed class FakeDistributedCache : IDistributedCache
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

    public byte[] Get(string key) => _entries.TryGetValue(key, out var value) ? value : null;

    public Task<byte[]> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _entries[key] = value;

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);

        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _entries.TryRemove(key, out _);

    /// <summary>
    /// Evicts every cached entry, simulating a full distributed-cache flush or eviction so that
    /// authority-of-record behavior can be verified against the durable store rather than the cache.
    /// </summary>
    public void Clear() => _entries.Clear();

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);

        return Task.CompletedTask;
    }
}
