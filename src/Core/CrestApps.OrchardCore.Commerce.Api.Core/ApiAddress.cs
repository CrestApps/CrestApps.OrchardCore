using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// A postal address. Which fields a country needs, and their names (state, province, prefecture), follow its own
/// address format; <c>country</c> is the ISO 3166-1 alpha-2 code.
/// </summary>
public sealed record ApiAddress(
    string Name,
    string Company,
    string AddressLine1,
    string AddressLine2,
    string DependentLocality,
    string City,
    string Region,
    string PostalCode,
    string SortingCode,
    string Country,
    string Phone)
{
    public static ApiAddress From(Address address)
        => address is null
            ? null
            : new(
                address.Name,
                address.Company,
                address.AddressLine1,
                address.AddressLine2,
                address.DependentLocality,
                address.City,
                address.Region,
                address.PostalCode,
                address.SortingCode,
                address.Country,
                address.Phone);
}
