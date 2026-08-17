namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// Represents a postal or geographic address. Components are optional; empty components are simply
/// ignored by consumers such as tax jurisdiction resolution. No particular hierarchy is assumed.
/// This is a money-safe value snapshot: its components are init-only, so once an address is constructed
/// it is immutable and a snapshot captured on an order or shipment can never be changed by later edits to
/// the customer-editable address content item it was resolved from. Use <see cref="Clone"/> to derive a
/// modified copy.
/// </summary>
public sealed class Address
{
    /// <summary>
    /// Gets or sets the recipient full name (the person or attention line the address is addressed to).
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the company or organization name, when the address belongs to a business.
    /// </summary>
    public string Company { get; init; }

    /// <summary>
    /// Gets or sets the first street line (street number and name, or PO box).
    /// </summary>
    public string AddressLine1 { get; init; }

    /// <summary>
    /// Gets or sets the second street line (apartment, suite, unit, or building), when present.
    /// </summary>
    public string AddressLine2 { get; init; }

    /// <summary>
    /// Gets or sets the ISO country code (for example <c>US</c> or <c>CA</c>).
    /// </summary>
    public string Country { get; init; }

    /// <summary>
    /// Gets or sets the state, province, or region code.
    /// </summary>
    public string Region { get; init; }

    /// <summary>
    /// Gets or sets the county name or code.
    /// </summary>
    public string County { get; init; }

    /// <summary>
    /// Gets or sets the city name.
    /// </summary>
    public string City { get; init; }

    /// <summary>
    /// Gets or sets the special or tax district identifier.
    /// </summary>
    public string District { get; init; }

    /// <summary>
    /// Gets or sets the postal or ZIP code.
    /// </summary>
    public string PostalCode { get; init; }

    /// <summary>
    /// Gets or sets the contact phone number for the address, used for delivery and fulfillment.
    /// </summary>
    public string Phone { get; init; }

    /// <summary>
    /// Creates a shallow copy of the current address.
    /// </summary>
    /// <returns>A new <see cref="Address"/> with the same values.</returns>
    public Address Clone()
    {
        return new Address
        {
            Name = Name,
            Company = Company,
            AddressLine1 = AddressLine1,
            AddressLine2 = AddressLine2,
            Country = Country,
            Region = Region,
            County = County,
            City = City,
            District = District,
            PostalCode = PostalCode,
            Phone = Phone,
        };
    }
}
