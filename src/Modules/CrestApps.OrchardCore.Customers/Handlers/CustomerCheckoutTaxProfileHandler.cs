using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Tells checkout tax who is buying: a business or a person, their tax id, whether they are exempt, and the exemption
/// certificates on file for them. Only a signed-in buyer is known; a guest is taxed as a person. When another handler
/// already said who is buying, such as a business account the buyer buys for, its type and tax numbers stay, and the
/// customer record adds what is missing: the customer, the exemption and the certificates. The rules are the ones the
/// tax profile of a customer party uses (<see cref="CustomerTaxProfiles"/>).
/// </summary>
internal sealed class CustomerCheckoutTaxProfileHandler : ICheckoutTaxProfileHandler
{
    private readonly ICustomerStore _customers;
    private readonly IServiceProvider _serviceProvider;

    public CustomerCheckoutTaxProfileHandler(ICustomerStore customers, IServiceProvider serviceProvider)
    {
        _customers = customers;
        _serviceProvider = serviceProvider;
    }

    public async Task BuildingAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId } ||
            (profile.Customer is not null && !string.IsNullOrEmpty(profile.Customer.CustomerId)))
        {
            return;
        }

        var customer = await _customers.FindByUserIdAsync(ownerId, cancellationToken);

        if (customer is null)
        {
            return;
        }

        if (profile.Customer is { } known)
        {
            await CustomerTaxProfiles.MergeAsync(known, customer, _serviceProvider, cancellationToken);

            return;
        }

        profile.Customer = await CustomerTaxProfiles.CreateAsync(customer, _serviceProvider, cancellationToken);
    }
}
