using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The places an account has, such as its head office, its warehouses or the sites where it is serviced. Modules offer
/// them wherever an address for the account is asked for, such as the shipping address at checkout.
/// </summary>
public sealed class AccountLocationsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the account's locations.
    /// </summary>
    public List<AccountLocation> Locations { get; set; } = [];

    /// <summary>
    /// Returns the location with the id, or <see langword="null"/>.
    /// </summary>
    /// <param name="locationId">The location id.</param>
    public AccountLocation FindLocation(string locationId)
        => string.IsNullOrEmpty(locationId)
            ? null
            : Locations?.FirstOrDefault(location => string.Equals(location.LocationId, locationId, StringComparison.Ordinal));

    /// <summary>
    /// Returns the location the account is billed at: the one marked <see cref="AccountLocation.IsBilling"/>, else the
    /// first location, or <see langword="null"/> when the account has none.
    /// </summary>
    public AccountLocation GetBillingLocation()
    {
        AccountLocation first = null;

        foreach (var location in Locations ?? [])
        {
            if (location is null)
            {
                continue;
            }

            if (location.IsBilling)
            {
                return location;
            }

            first ??= location;
        }

        return first;
    }
}

/// <summary>
/// A place an account has.
/// </summary>
public sealed class AccountLocation
{
    /// <summary>
    /// Gets or sets the location id, which stays the same when the location is renamed or moved.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets the location's name, such as "Head office".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the location's address, addressed to the account.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account's invoices and other billing documents are addressed to this
    /// location. An account has one billing location at most; without one, the first location is used.
    /// </summary>
    public bool IsBilling { get; set; }
}
