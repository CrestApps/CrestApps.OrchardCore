using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Offers the locations of the account a buyer buys for at the checkout's Shipping step, next to the buyer's own
/// address book, while the account buys as a business.
/// </summary>
internal sealed class AccountLocationsShippingAddressSource : IShippingAddressSource
{
    internal const string AddressIdPrefix = "account-location-";

    private readonly B2BAccountService _accounts;

    public AccountLocationsShippingAddressSource(B2BAccountService accounts)
    {
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<ShippingAddressOption>> GetAddressesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await _accounts.GetForUserAsync(userId, cancellationToken) is not { } account)
        {
            return [];
        }

        return [.. account.Locations
            .Where(location => !string.IsNullOrEmpty(location.LocationId) && !string.IsNullOrWhiteSpace(location.Address?.AddressLine1))
            .Select(location => new ShippingAddressOption
            {
                AddressId = AddressIdPrefix + location.LocationId,
                Label = location.Name,
                Description = account.Name,
                Address = WithoutRepeatedName(location.Address),
            })];
    }

    // A location saved with the account's name as both its recipient and its company shows it once.
    private static Address WithoutRepeatedName(Address address)
        => !string.IsNullOrEmpty(address.Name) && string.Equals(address.Name, address.Company, StringComparison.OrdinalIgnoreCase)
            ? new Address
            {
                Company = address.Company,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                DependentLocality = address.DependentLocality,
                SortingCode = address.SortingCode,
                Country = address.Country,
                Region = address.Region,
                County = address.County,
                City = address.City,
                District = address.District,
                PostalCode = address.PostalCode,
                Phone = address.Phone,
            }
            : address;
}
