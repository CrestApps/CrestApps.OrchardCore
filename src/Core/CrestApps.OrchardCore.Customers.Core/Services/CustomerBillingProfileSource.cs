using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Bills a commerce customer (party kind <see cref="PartyKinds.Customer"/>) from their customer record: their name,
/// e-mail and phone, their default billing address, their default shipping address when it is another one, their tax
/// id and whether they are exempt.
/// </summary>
public sealed class CustomerBillingProfileSource : IPartyBillingProfileSource
{
    private readonly ICustomerStore _customerStore;
    private readonly IPhoneNumberService _phoneNumberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerBillingProfileSource"/> class.
    /// </summary>
    /// <param name="customerStore">The customer store.</param>
    /// <param name="phoneNumberService">The phone number service that turns a phone typed in another form into E.164, read with the country of the billing address; <see langword="null"/> when the Phone Numbers feature is off.</param>
    public CustomerBillingProfileSource(ICustomerStore customerStore, IPhoneNumberService phoneNumberService = null)
    {
        _customerStore = customerStore;
        _phoneNumberService = phoneNumberService;
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
        var phone = PhoneNumberNormalization.ToE164OrTrimmed(customer.Phone, _phoneNumberService, billing?.Country);
        var found = ContactInfo.From(customer.GetName(), customer.Email, phone, billing);
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
