using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Commerce.Stores;

/// <summary>
/// Adds places the store works from to <see cref="IStoreLocationService.ListLocationsAsync"/>, such as the inventory's
/// stock locations, shipping's pickup points or the point of sale's registers. Each place's id is prefixed by its
/// source, such as <c>inventory:warehouse-1</c>, so ids never collide.
/// </summary>
public interface IStoreLocationSource
{
    /// <summary>
    /// Returns the source's places.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NamedAddress>> GetLocationsAsync(CancellationToken cancellationToken = default);
}
