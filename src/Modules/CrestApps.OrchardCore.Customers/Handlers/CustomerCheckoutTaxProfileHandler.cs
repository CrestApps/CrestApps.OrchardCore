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
/// certificates on file for them. Only a signed-in buyer is known; a guest is taxed as a person. When another handler
/// already said who is buying, such as a business account the buyer buys for, its type and tax numbers stay, and the
/// customer record adds what is missing: the customer, the exemption and the certificates.
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
            known.CustomerId = customer.ItemId;
            known.TaxRegistrationNumber ??= customer.TaxId;
            known.VatNumber ??= customer.TaxId;
            known.TaxRegistrationVerified |= IsVerified(customer);
            known.IsTaxExempt |= customer.IsTaxExempt;
            known.ExemptionCertificateIds = [.. (known.ExemptionCertificateIds ?? []).Union(await GetCertificateIdsAsync(customer.ItemId, cancellationToken))];

            return;
        }

        profile.Customer = new CustomerTaxProfile
        {
            CustomerId = customer.ItemId,
            // The customer sets their own kind, and tax rules are scoped by it, so a self-declared business would step
            // out of a consumer-only rule. Only a verified business number makes the buyer a business for tax; a store-
            // managed business account says so through its own handler, before this one.
            CustomerType = customer.Kind == CustomerKind.Business && IsVerified(customer) ? CustomerTaxType.B2B : CustomerTaxType.B2C,
            TaxRegistrationNumber = customer.TaxId,
            VatNumber = customer.TaxId,
            TaxRegistrationVerified = IsVerified(customer),
            IsTaxExempt = customer.IsTaxExempt,
            ExemptionCertificateIds = await GetCertificateIdsAsync(customer.ItemId, cancellationToken),
        };
    }

    // Only a number an administrator verified counts: the customer chooses their kind and types their number themselves.
    private static bool IsVerified(Customer customer)
        => customer.TaxIdVerified && !string.IsNullOrWhiteSpace(customer.TaxId);

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
