namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Caches small, slowly-changing routing configuration (such as enabled queues, skills, business-hours calendars, and
/// entry points) so that latency-critical routing decisions do not re-query the database on every call. Cached
/// snapshots are invalidated through <see cref="OrchardCore.Environment.Cache.ISignal"/> change tokens whenever the
/// underlying configuration is written, keeping every process instance on a shared tenant cache consistent.
/// </summary>
public interface IContactCenterConfigurationCache
{
    /// <summary>
    /// Returns the cached collection of enabled entries of type <typeparamref name="T"/>, populating the cache from the
    /// supplied factory on a miss. The returned snapshot is shared across callers and must be treated as read-only.
    /// </summary>
    /// <typeparam name="T">The configuration entry type being cached.</typeparam>
    /// <param name="factory">The factory used to load the enabled entries when the cache is empty or invalidated.</param>
    /// <param name="cancellationToken">A token used to cancel the load operation.</param>
    /// <returns>A task that resolves to the cached, read-only collection of enabled entries.</returns>
    Task<IReadOnlyCollection<T>> GetEnabledAsync<T>(
        Func<CancellationToken, Task<IReadOnlyCollection<T>>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached collection of enabled entries of type <typeparamref name="T"/> so the next read reloads
    /// it from the underlying store. Invalidation is signalled across every process instance sharing the tenant cache.
    /// </summary>
    /// <typeparam name="T">The configuration entry type whose cached snapshot should be invalidated.</typeparam>
    /// <returns>A task that completes when the invalidation signal has been raised.</returns>
    Task InvalidateEnabledAsync<T>();
}
