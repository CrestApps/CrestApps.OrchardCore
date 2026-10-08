using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// An <see cref="IDistributedCache"/> that throws on every operation, simulating a distributed-cache
/// transport outage (for example Redis being unavailable). It proves that security-critical paths such as
/// credential revocation and cleanup treat the cache as a pure performance cache and never depend on its
/// availability, always resolving authority from the durable lease store instead.
/// </summary>
internal sealed class ThrowingDistributedCache : IDistributedCache
{
    private static InvalidOperationException Outage()
        => new("The distributed cache is unavailable.");

    public byte[] Get(string key) => throw Outage();

    public Task<byte[]> GetAsync(string key, CancellationToken token = default) => throw Outage();

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Outage();

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
        => throw Outage();

    public void Refresh(string key) => throw Outage();

    public Task RefreshAsync(string key, CancellationToken token = default) => throw Outage();

    public void Remove(string key) => throw Outage();

    public Task RemoveAsync(string key, CancellationToken token = default) => throw Outage();
}
