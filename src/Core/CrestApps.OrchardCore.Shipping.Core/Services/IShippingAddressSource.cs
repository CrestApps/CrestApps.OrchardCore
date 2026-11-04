using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Offers addresses a signed-in buyer can ship to besides their own address book, such as the locations of the business
/// account they buy for. The checkout's Shipping step lists them with the buyer's saved addresses, and ships to the one
/// chosen as it is, without asking for it again.
/// </summary>
public interface IShippingAddressSource
{
    /// <summary>
    /// Returns the addresses the user can ship to, or none.
    /// </summary>
    /// <param name="userId">The signed-in buyer's user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<ShippingAddressOption>> GetAddressesAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// An address a buyer can ship to, offered by an <see cref="IShippingAddressSource"/>.
/// </summary>
public sealed class ShippingAddressOption
{
    /// <summary>
    /// Gets or sets the option's id, unique across sources, such as <c>account-location-{id}</c>.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the address's name, such as "Head office".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets where the address comes from, such as the account's name.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the address, complete enough to ship to.
    /// </summary>
    public Address Address { get; set; }
}
