using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;

namespace CrestApps.OrchardCore.TimeZones.AI;

/// <summary>
/// The time zone maps the tools saved or deleted in the current scope. Every tool call of one chat message runs in one
/// scope, and the time zone map catalog reads a cached document that is refreshed only when the scope commits, so a map
/// created earlier in the message would otherwise not be found, and two saves of one name would both look new.
/// </summary>
internal sealed class TimeZoneMapScopeChanges
{
    private readonly Dictionary<string, TimeZoneMap> _saved = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deleted = new(StringComparer.Ordinal);

    /// <summary>
    /// Records a map created or updated in this scope.
    /// </summary>
    /// <param name="map">The map as it was saved.</param>
    public void Saved(TimeZoneMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _deleted.Remove(map.ItemId);
        _saved[map.ItemId] = map.Clone();
    }

    /// <summary>
    /// Records a map deleted in this scope.
    /// </summary>
    /// <param name="itemId">The map's identifier.</param>
    public void Deleted(string itemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);

        _saved.Remove(itemId);
        _deleted.Add(itemId);
    }

    /// <summary>
    /// Reads every map, with the changes made in this scope.
    /// </summary>
    /// <param name="catalog">The time zone map catalog.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maps.</returns>
    public async Task<IReadOnlyList<TimeZoneMap>> GetAllAsync(INamedCatalog<TimeZoneMap> catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var stored = await catalog.GetAllAsync(cancellationToken);

        return
        [
            .. stored.Where(map => !_deleted.Contains(map.ItemId) && !_saved.ContainsKey(map.ItemId)),
            .. _saved.Values.Select(map => map.Clone()),
        ];
    }
}
