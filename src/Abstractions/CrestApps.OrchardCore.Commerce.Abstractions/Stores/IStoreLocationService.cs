using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Commerce.Stores;

/// <summary>
/// The store's identity and its places: the store profile, the store's own address, and every place the store works
/// from (its address and the places each <see cref="IStoreLocationSource"/> adds, such as warehouses, pickup points and
/// registers). Shipping origin, tax origin and receipts fall back to it when their own settings are empty.
/// </summary>
public interface IStoreLocationService
{
    /// <summary>
    /// Returns the store's profile, with its name falling back to the site's name. Never <see langword="null"/>.
    /// </summary>
    Task<StoreProfileSettings> GetProfileAsync();

    /// <summary>
    /// Returns the store's address, or <see langword="null"/> when the profile has none.
    /// </summary>
    Task<Address> GetAddressAsync();

    /// <summary>
    /// Returns the store's address on one line, written the way its country writes addresses, or
    /// <see langword="null"/> when the profile has none.
    /// </summary>
    Task<string> GetAddressTextAsync();

    /// <summary>
    /// Returns every place the store works from: its own address first, as <see cref="StoreLocationIds.Store"/>, and
    /// then the places each <see cref="IStoreLocationSource"/> adds.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<NamedAddress>> ListLocationsAsync(CancellationToken cancellationToken = default);
}
