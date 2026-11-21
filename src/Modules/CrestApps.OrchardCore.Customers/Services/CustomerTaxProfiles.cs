using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// What a customer record says about the buyer's tax status: a business or a person, their tax id, whether they are
/// exempt, and the exemption certificates on file for them. Checkout tax and the tax profile of a customer party both
/// read it here, so the rules exist once.
/// </summary>
internal static class CustomerTaxProfiles
{
    /// <summary>
    /// Returns the tax type of a customer: a business customer with a tax number the store verified is taxed as a
    /// business, anyone else as a person. The customer sets their own kind, and tax rules are scoped by it, so a
    /// self-declared business would step out of a consumer-only rule; a store-managed business account says so through
    /// its own handler.
    /// </summary>
    public static CustomerTaxType TypeOf(Customer customer)
        => customer.Kind == CustomerKind.Business && IsVerified(customer) ? CustomerTaxType.B2B : CustomerTaxType.B2C;

    /// <summary>
    /// Returns whether the customer's tax number counts as verified. Only a number an administrator verified counts: the
    /// customer chooses their kind and types their number themselves.
    /// </summary>
    public static bool IsVerified(Customer customer)
        => customer.TaxIdVerified && !string.IsNullOrWhiteSpace(customer.TaxId);

    /// <summary>
    /// Returns a new profile of the customer, for a buyer nothing else is known about.
    /// </summary>
    public static async Task<CustomerTaxProfile> CreateAsync(Customer customer, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => new()
        {
            CustomerId = customer.ItemId,
            CustomerType = TypeOf(customer),
            TaxRegistrationNumber = customer.TaxId,
            VatNumber = customer.TaxId,
            TaxRegistrationVerified = IsVerified(customer),
            IsTaxExempt = customer.IsTaxExempt,
            ExemptionCertificateIds = await GetCertificateIdsAsync(customer.ItemId, serviceProvider, cancellationToken),
        };

    /// <summary>
    /// Adds the customer to a profile something else already filled, such as the business account the buyer buys for:
    /// its type and tax numbers stay, and the customer adds what is missing, the customer id, the exemption and the
    /// certificates.
    /// </summary>
    public static async Task MergeAsync(CustomerTaxProfile known, Customer customer, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        known.CustomerId = customer.ItemId;
        known.TaxRegistrationNumber ??= customer.TaxId;
        known.VatNumber ??= customer.TaxId;
        known.TaxRegistrationVerified |= IsVerified(customer);
        known.IsTaxExempt |= customer.IsTaxExempt;
        known.ExemptionCertificateIds = [.. (known.ExemptionCertificateIds ?? []).Union(await GetCertificateIdsAsync(customer.ItemId, serviceProvider, cancellationToken))];
    }

    // Certificates are kept by the Taxation feature, which may be off.
    private static async Task<IList<string>> GetCertificateIdsAsync(string customerId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService<IExemptionCertificateStore>() is not { } certificates)
        {
            return [];
        }

        return [.. (await certificates.GetAllAsync(cancellationToken))
            .Where(certificate => string.Equals(certificate.CustomerId, customerId, StringComparison.Ordinal))
            .Select(certificate => certificate.ItemId)];
    }
}
