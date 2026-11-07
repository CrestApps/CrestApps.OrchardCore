using CrestApps.Core;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The buyer's address book, kept on their customer record, for checkout steps such as Shipping.
/// </summary>
public sealed class CustomerShippingAddressBook : IShippingAddressBook
{
    private readonly ICustomerService _customers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerShippingAddressBook"/> class.
    /// </summary>
    /// <param name="customers">The customer service.</param>
    public CustomerShippingAddressBook(ICustomerService customers)
    {
        _customers = customers;
    }

    /// <inheritdoc/>
    public async Task<ShippingAddressBook> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || await _customers.GetOrCreateForUserAsync(userId, cancellationToken) is not { } customer)
        {
            return null;
        }

        return new ShippingAddressBook
        {
            Addresses = [.. (customer.Addresses ?? []).Select(entry => new ShippingAddressBookEntry
            {
                AddressId = entry.AddressId,
                Label = entry.Label,
                Address = entry.Address,
            })],
            DefaultShippingAddressId = customer.DefaultShippingAddressId,
            DefaultBillingAddressId = customer.DefaultBillingAddressId,
            MaxAddresses = CustomerAddressBook.MaxAddresses,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> AddAsync(string userId, Address address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || address is null || await _customers.GetOrCreateForUserAsync(userId, cancellationToken) is not { } customer)
        {
            return false;
        }

        if ((customer.Addresses?.Count ?? 0) >= CustomerAddressBook.MaxAddresses ||
            (customer.Addresses ?? []).Any(entry => AddressComparison.IsSameAddress(entry.Address, address)))
        {
            return false;
        }

        var entry = new CustomerAddress
        {
            AddressId = UniqueId.GenerateId(),
            Address = address,
        };

        customer.Addresses ??= [];
        customer.Addresses.Add(entry);
        customer.DefaultShippingAddressId ??= entry.AddressId;

        await _customers.TouchAsync(customer, cancellationToken);

        return true;
    }
}
