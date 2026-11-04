namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// One address saved in a buyer's address book.
/// </summary>
public sealed class ShippingAddressBookEntry
{
    /// <summary>
    /// Gets or sets the address id, unique in the book.
    /// </summary>
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the name the buyer gave the address, such as "Home", or <see langword="null"/>.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Returns a copy of the address, or an empty address.
    /// </summary>
    /// <returns>The address.</returns>
    public Address ToAddress()
        => Address?.Clone() ?? new Address();
}
