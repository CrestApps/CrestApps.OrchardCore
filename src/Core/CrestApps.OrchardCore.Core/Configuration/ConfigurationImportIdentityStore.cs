using System.Collections.Concurrent;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Holds the identifier substitutions made while a recipe is importing configuration, for the length of that recipe.
/// </summary>
/// <remarks>
/// A recipe step can execute in a scope of its own, so the substitutions one step makes have to outlive the scope
/// that made them or the steps that follow will write references to entries the destination does not have. The store
/// is therefore held for the tenant and keyed by the recipe's execution identifier. An import that never reaches its
/// last step would otherwise leave its map behind forever, so a map that has not been touched for the retention
/// window is discarded the next time the store is used; a recipe that outlives that window has stopped being an
/// import in progress by any reasonable measure.
/// </remarks>
public sealed class ConfigurationImportIdentityStore
{
    /// <summary>
    /// The period a map is kept after its last use before it is treated as abandoned.
    /// </summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationImportIdentityStore"/> class.
    /// </summary>
    /// <param name="clock">The clock used to decide when a map has been abandoned.</param>
    public ConfigurationImportIdentityStore(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Gets the map for a recipe execution, creating it the first time the execution needs one.
    /// </summary>
    /// <param name="executionId">The identifier of the executing recipe.</param>
    /// <returns>The map shared by every step of that recipe.</returns>
    public ConfigurationImportIdentityMap GetOrCreate(string executionId)
    {
        var now = _clock.UtcNow;

        Evict(now);

        var entry = _entries.AddOrUpdate(
            executionId ?? string.Empty,
            _ => new Entry(new ConfigurationImportIdentityMap(), now),
            (_, existing) => existing with { LastUsedUtc = now });

        return entry.Map;
    }

    /// <summary>
    /// Discards the map for a recipe execution once that recipe can no longer add to it.
    /// </summary>
    /// <param name="executionId">The identifier of the recipe whose map is no longer needed.</param>
    public void Release(string executionId)
    {
        _entries.TryRemove(executionId ?? string.Empty, out _);
    }

    private void Evict(DateTime now)
    {
        foreach (var pair in _entries)
        {
            if (now - pair.Value.LastUsedUtc > RetentionWindow)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record Entry(ConfigurationImportIdentityMap Map, DateTime LastUsedUtc);
}
