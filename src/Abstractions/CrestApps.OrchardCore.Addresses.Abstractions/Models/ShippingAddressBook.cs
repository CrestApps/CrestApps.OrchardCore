namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// A buyer's address book, as an <see cref="Services.IShippingAddressBook"/> returns it.
/// </summary>
public sealed class ShippingAddressBook
{
    /// <summary>
    /// Gets or sets the saved addresses.
    /// </summary>
    public IReadOnlyList<ShippingAddressBookEntry> Addresses { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the default shipping address, or <see langword="null"/>.
    /// </summary>
    public string DefaultShippingAddressId { get; set; }

    /// <summary>
    /// Gets or sets the id of the default billing address, or <see langword="null"/>.
    /// </summary>
    public string DefaultBillingAddressId { get; set; }

    /// <summary>
    /// Gets or sets how many addresses the book holds at most.
    /// </summary>
    public int MaxAddresses { get; set; }

    /// <summary>
    /// Gets whether another address can be saved.
    /// </summary>
    public bool CanAdd
        => (Addresses?.Count ?? 0) < MaxAddresses;

    /// <summary>
    /// Returns a saved address by its id, or <see langword="null"/>.
    /// </summary>
    /// <param name="addressId">The address id.</param>
    /// <returns>The address.</returns>
    public ShippingAddressBookEntry FindAddress(string addressId)
        => string.IsNullOrEmpty(addressId)
            ? null
            : Addresses?.FirstOrDefault(entry => string.Equals(entry.AddressId, addressId, StringComparison.Ordinal));

    /// <summary>
    /// Returns the default shipping address, or <see langword="null"/>.
    /// </summary>
    /// <returns>The address.</returns>
    public ShippingAddressBookEntry GetDefaultShippingAddress()
        => FindAddress(DefaultShippingAddressId);

    /// <summary>
    /// Returns the default billing address, or <see langword="null"/>.
    /// </summary>
    /// <returns>The address.</returns>
    public ShippingAddressBookEntry GetDefaultBillingAddress()
        => FindAddress(DefaultBillingAddressId);
}
