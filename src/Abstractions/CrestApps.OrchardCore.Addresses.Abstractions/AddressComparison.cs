using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Compares postal addresses as places.
/// </summary>
public static class AddressComparison
{
    /// <summary>
    /// Returns whether two addresses are the same place: the same street lines, city, region, postal code and country,
    /// ignoring case and surrounding spaces, and ignoring who they are addressed to. Used to keep an address book from
    /// saving the same address twice.
    /// </summary>
    /// <param name="left">The first address.</param>
    /// <param name="right">The second address.</param>
    /// <returns><see langword="false"/> when either is <see langword="null"/>.</returns>
    public static bool IsSameAddress(Address left, Address right)
        => left is not null && right is not null &&
            Same(left.AddressLine1, right.AddressLine1) &&
            Same(left.AddressLine2, right.AddressLine2) &&
            Same(left.City, right.City) &&
            Same(left.Region, right.Region) &&
            Same(left.PostalCode, right.PostalCode) &&
            Same(left.Country, right.Country);

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
