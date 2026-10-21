using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Tells checkout tax who is buying: a business or a person, their tax id, whether they are exempt, and the exemption
/// certificates on file for them. Only a signed-in buyer is known; a guest is taxed as a person.
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

        if (profile.Customer is not null || session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId })
        {
            return;
        }

        var customer = await _customers.FindByUserIdAsync(ownerId, cancellationToken);

        if (customer is null)
        {
            return;
        }

        profile.Customer = new CustomerTaxProfile
        {
            CustomerId = customer.ItemId,
            CustomerType = customer.Kind == CustomerKind.Business ? CustomerTaxType.B2B : CustomerTaxType.B2C,
            TaxRegistrationNumber = customer.TaxId,
            VatNumber = customer.TaxId,
            IsTaxExempt = customer.IsTaxExempt,
            ExemptionCertificateIds = await GetCertificateIdsAsync(customer.ItemId, cancellationToken),
        };
    }

    // Certificates are kept by the Taxation feature, which may be off.
    private async Task<IList<string>> GetCertificateIdsAsync(string customerId, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IExemptionCertificateStore>() is not { } certificates)
        {
            return [];
        }

        return [.. (await certificates.GetAllAsync(cancellationToken))
            .Where(certificate => string.Equals(certificate.CustomerId, customerId, StringComparison.Ordinal))
            .Select(certificate => certificate.ItemId)];
    }
}
