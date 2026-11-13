using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Bills a commerce customer (party kind <see cref="PartyKinds.Customer"/>) from their customer record: their name,
/// e-mail and phone, their default billing address, their default shipping address when it is another one, their tax
/// id and whether they are exempt.
/// </summary>
public sealed class CustomerBillingProfileSource : IPartyBillingProfileSource
{
    private readonly ICustomerStore _customerStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerBillingProfileSource"/> class.
    /// </summary>
    /// <param name="customerStore">The customer store.</param>
    public CustomerBillingProfileSource(ICustomerStore customerStore)
    {
        _customerStore = customerStore;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Customer;

    /// <inheritdoc/>
    public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty || await _customerStore.FindByIdAsync(party.Id, cancellationToken) is not { } customer)
        {
            return;
        }

        var billing = customer.GetDefaultBillingAddress()?.ToAddress();
        var shipping = customer.GetDefaultShippingAddress()?.ToAddress();
        var found = ContactInfo.From(customer.GetName(), customer.Email, customer.Phone, billing);
        var contact = profile.Contact ?? new ContactInfo();

        profile.Contact = contact with
        {
            Name = found.Name ?? contact.Name,
            Email = found.Email ?? contact.Email,
            Phone = found.Phone.HasValue ? found.Phone : contact.Phone,
            Address = found.Address ?? contact.Address,
        };

        if (shipping is not null && (billing is null || !AddressComparison.IsSameAddress(billing, shipping)))
        {
            profile.ShippingAddress = shipping;
        }

        if (!string.IsNullOrWhiteSpace(customer.TaxId))
        {
            profile.TaxId = customer.TaxId.Trim();
        }

        profile.TaxExempt |= customer.IsTaxExempt;
    }
}
