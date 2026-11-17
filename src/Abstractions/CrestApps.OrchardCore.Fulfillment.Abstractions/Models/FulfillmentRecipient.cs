namespace CrestApps.OrchardCore.Fulfillment.Models;

/// <summary>
/// Who a fulfillment partner ships an order to.
/// </summary>
public sealed class FulfillmentRecipient
{
    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the company.
    /// </summary>
    public string Company { get; set; }

    /// <summary>
    /// Gets or sets the first line of the street address.
    /// </summary>
    public string AddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the second line of the street address.
    /// </summary>
    public string AddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the region code without the country, such as <c>OH</c>.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country code, such as <c>US</c>.
    /// </summary>
    public string Country { get; set; }
}
