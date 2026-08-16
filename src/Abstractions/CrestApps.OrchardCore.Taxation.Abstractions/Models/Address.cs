namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a location used to determine the applicable tax jurisdictions. No particular hierarchy
/// is assumed; empty components are simply ignored during jurisdiction resolution.
/// </summary>
public sealed class Address
{
    /// <summary>
    /// Gets or sets the ISO country code (for example <c>US</c> or <c>CA</c>).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the state, province, or region code.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the county name or code.
    /// </summary>
    public string County { get; set; }

    /// <summary>
    /// Gets or sets the city name.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the special or tax district identifier.
    /// </summary>
    public string District { get; set; }

    /// <summary>
    /// Gets or sets the postal or ZIP code.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Creates a shallow copy of the current address.
    /// </summary>
    /// <returns>A new <see cref="Address"/> with the same values.</returns>
    public Address Clone()
    {
        return new Address
        {
            Country = Country,
            Region = Region,
            County = County,
            City = City,
            District = District,
            PostalCode = PostalCode,
        };
    }
}
