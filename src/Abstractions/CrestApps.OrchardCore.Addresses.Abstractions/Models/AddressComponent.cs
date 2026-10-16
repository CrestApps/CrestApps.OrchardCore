namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// The parts of an <see cref="Address"/> a country's format places, requires and checks.
/// </summary>
public enum AddressComponent
{
    Name,
    Company,

    /// <summary>
    /// Both street lines.
    /// </summary>
    StreetAddress,
    DependentLocality,
    City,
    Region,
    PostalCode,
    SortingCode,
    Country,
}
