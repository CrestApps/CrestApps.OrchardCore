using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// The address book a signed-in buyer ships from: the addresses they saved, their default addresses, and a way to save a
/// new one. The module that keeps buyers' address books (Customers) implements it, so checkout steps such as Shipping
/// list and save addresses without knowing where address books are kept.
/// </summary>
public interface IShippingAddressBook
{
    /// <summary>
    /// Returns the user's address book, or <see langword="null"/> when the user has none.
    /// </summary>
    /// <param name="userId">The signed-in buyer's user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ShippingAddressBook> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an address in the user's address book, unless the book is full or already holds the same address. The first
    /// address saved becomes the default shipping address.
    /// </summary>
    /// <param name="userId">The signed-in buyer's user id.</param>
    /// <param name="address">The address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the address was saved.</returns>
    Task<bool> AddAsync(string userId, Address address, CancellationToken cancellationToken = default);
}
