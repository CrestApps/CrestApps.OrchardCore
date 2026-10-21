namespace CrestApps.OrchardCore.Inventory.Core.Services;

/// <summary>
/// Answers which locations stock can be changed at.
/// </summary>
public static class InventoryLocationServiceExtensions
{
    /// <summary>
    /// Gets the identifiers of the listed locations.
    /// </summary>
    /// <param name="locations">The location service.</param>
    public static async Task<IReadOnlySet<string>> GetLocationIdsAsync(this IInventoryLocationService locations)
    {
        ArgumentNullException.ThrowIfNull(locations);

        return (await locations.GetLocationsAsync())
            .Select(location => location.LocationId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the location a change is made at: the given one when it's listed, otherwise the default one. A removed
    /// location only gives units away, by a transfer.
    /// </summary>
    /// <param name="locations">The location service.</param>
    /// <param name="locationId">The location asked for.</param>
    public static async Task<string> GetKnownLocationIdAsync(this IInventoryLocationService locations, string locationId)
        => KnownOrDefault(await locations.GetLocationIdsAsync(), locationId);

    /// <summary>
    /// Gets the given location when it's one of the known ones, otherwise the default one.
    /// </summary>
    /// <param name="knownLocationIds">The identifiers of the listed locations.</param>
    /// <param name="locationId">The location asked for.</param>
    public static string KnownOrDefault(IReadOnlySet<string> knownLocationIds, string locationId)
    {
        ArgumentNullException.ThrowIfNull(knownLocationIds);

        return !string.IsNullOrEmpty(locationId) && knownLocationIds.Contains(locationId)
            ? locationId
            : InventoryConstants.DefaultLocationId;
    }
}
