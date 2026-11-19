using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Adds a customer record to the tax profile of a customer party (party kind <see cref="PartyKinds.Customer"/>), with
/// the rules checkout tax uses (<see cref="CustomerTaxProfiles"/>): the customer id, the tax id and the exemption when
/// nothing else set them, the exemption certificates on file, and a business customer taxed as a business.
/// </summary>
internal sealed class CustomerPartyTaxProfileProvider : IPartyTaxProfileProvider
{
    private readonly ICustomerStore _customers;
    private readonly IServiceProvider _serviceProvider;

    public CustomerPartyTaxProfileProvider(ICustomerStore customers, IServiceProvider serviceProvider)
    {
        _customers = customers;
        _serviceProvider = serviceProvider;
    }

    public async Task BuildAsync(CustomerTaxProfile profile, PartyRef party, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty ||
            !string.Equals(party.Kind, PartyKinds.Customer, StringComparison.OrdinalIgnoreCase) ||
            await _customers.FindByIdAsync(party.Id, cancellationToken) is not { } customer)
        {
            return;
        }

        await CustomerTaxProfiles.MergeAsync(profile, customer, _serviceProvider, cancellationToken);

        if (CustomerTaxProfiles.TypeOf(customer) == CustomerTaxType.B2B)
        {
            profile.CustomerType = CustomerTaxType.B2B;
        }
    }
}
