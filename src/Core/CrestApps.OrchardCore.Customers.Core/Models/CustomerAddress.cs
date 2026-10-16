using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// An address in a customer's address book. It can be edited and deleted; an order keeps its own copy, made with
/// <see cref="ToAddress"/> when it is placed, so a change here never changes an order.
/// </summary>
public sealed class CustomerAddress
{
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the customer's own name for the address, such as Home or Office.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets who receives mail at the address.
    /// </summary>
    public string Name { get; set; }

    public string Company { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string City { get; set; }

    /// <summary>
    /// Gets or sets the state, province or region.
    /// </summary>
    public string Region { get; set; }

    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 code of the country, such as US.
    /// </summary>
    public string Country { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// The immutable copy an order keeps.
    /// </summary>
    public Address ToAddress()
        => new()
        {
            Name = Name,
            Company = Company,
            AddressLine1 = AddressLine1,
            AddressLine2 = AddressLine2,
            City = City,
            Region = Region,
            PostalCode = PostalCode,
            Country = Country,
            Phone = Phone,
        };

    /// <summary>
    /// The address as lines, the way an envelope reads, with the country's name from <paramref name="countryName"/>.
    /// </summary>
    public IReadOnlyList<string> GetLines(Func<string, string> countryName = null)
    {
        var place = string.Join(' ', new[] { City, Region, PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new[]
        {
            Name,
            Company,
            AddressLine1,
            AddressLine2,
            place,
            string.IsNullOrEmpty(Country) ? null : countryName?.Invoke(Country) ?? Country,
        }
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToArray();
    }
}
