using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// A distributed cache that records each read, write and removal it is asked for, in order, and passes it on.
/// </summary>
internal sealed class RecordingDistributedCache : IDistributedCache
{
    private readonly IDistributedCache _inner;
    private readonly List<string> _operations = [];

    public RecordingDistributedCache(IDistributedCache inner)
        => _inner = inner;

    /// <summary>
    /// The operations made so far: "Get", "Set", "Refresh" or "Remove".
    /// </summary>
    public IReadOnlyList<string> Operations
    {
        get
        {
            lock (_operations)
            {
                return [.. _operations];
            }
        }
    }

    public void Clear()
    {
        lock (_operations)
        {
            _operations.Clear();
        }
    }

    public byte[] Get(string key)
    {
        Record("Get");

        return _inner.Get(key);
    }

    public Task<byte[]> GetAsync(string key, CancellationToken token = default)
    {
        Record("Get");

        return _inner.GetAsync(key, token);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        Record("Set");
        _inner.Set(key, value, options);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Record("Set");

        return _inner.SetAsync(key, value, options, token);
    }

    public void Refresh(string key)
    {
        Record("Refresh");
        _inner.Refresh(key);
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        Record("Refresh");

        return _inner.RefreshAsync(key, token);
    }

    public void Remove(string key)
    {
        Record("Remove");
        _inner.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Record("Remove");

        return _inner.RemoveAsync(key, token);
    }

    private void Record(string operation)
    {
        lock (_operations)
        {
            _operations.Add(operation);
        }
    }
}
