using CrestApps.Core.ContactCenter;

using CrestApps.Core.ContactCenter.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterConfigurationCache"/> using shell-scoped state
/// guarded by change tokens. The service is registered as a per-tenant singleton, so cached
/// snapshots are naturally tenant-isolated, and invalidation flows through <see cref="IContactCenterConfigurationChangeNotifier"/> so it is honored
/// across every process instance the host reaches.
/// </summary>
public sealed class ContactCenterConfigurationCache : IContactCenterConfigurationCache
{
    private const string CacheKeyPrefix = "CrestApps:ContactCenter:EnabledConfiguration:";

    private readonly IContactCenterConfigurationChangeNotifier _changeNotifier;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterConfigurationCache"/> class.
    /// </summary>
    /// <param name="changeNotifier">The notifier used to create and trip change tokens for invalidation.</param>
    public ContactCenterConfigurationCache(IContactCenterConfigurationChangeNotifier changeNotifier)
    {
        _changeNotifier = changeNotifier;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<T>> GetEnabledAsync<T>(
        Func<CancellationToken, Task<IReadOnlyCollection<T>>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var key = GetKey<T>();

        if (_entries.TryGetValue(key, out var cached) && !cached.Token.HasChanged)
        {
            return (IReadOnlyCollection<T>)cached.Value;
        }

        // Capture the change token before loading so that a concurrent write which trips the token while the factory
        // is running immediately marks the entry we are about to store as stale, forcing the next read to reload.
        var changeToken = _changeNotifier.Watch(key);

        var value = await factory(cancellationToken);

        _entries[key] = new CacheEntry(changeToken, value);

        return value;
    }

    /// <inheritdoc/>
    public Task InvalidateEnabledAsync<T>()
        => _changeNotifier.NotifyChangedAsync(GetKey<T>());

    private static string GetKey<T>()
        => CacheKeyPrefix + typeof(T).FullName;

    private sealed record CacheEntry(IChangeToken Token, object Value);
}
