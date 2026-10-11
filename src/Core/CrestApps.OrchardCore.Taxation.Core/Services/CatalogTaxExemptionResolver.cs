using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxExemptionResolver"/> backed by the <see cref="IExemptionCertificateStore"/>.
/// A customer is exempt when it is flagged tax exempt, or when it holds an active, effective certificate
/// that covers the tax type, jurisdiction, and classification of the rule.
/// </summary>
public sealed class CatalogTaxExemptionResolver : ITaxExemptionResolver
{
    private readonly IExemptionCertificateStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTaxExemptionResolver"/> class.
    /// </summary>
    /// <param name="store">The exemption certificate store.</param>
    public CatalogTaxExemptionResolver(IExemptionCertificateStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsExemptAsync(CustomerTaxProfile customer, TaxRule rule, DateTime onUtc, CancellationToken cancellationToken = default)
    {
        if (customer is null)
        {
            return false;
        }

        if (customer.IsTaxExempt)
        {
            return true;
        }

        if (customer.ExemptionCertificateIds is null || customer.ExemptionCertificateIds.Count == 0)
        {
            return false;
        }

        var certificates = await _store.GetAsync(customer.ExemptionCertificateIds, cancellationToken);

        return certificates.Any(certificate => Covers(certificate, rule, onUtc));
    }

    private static bool Covers(ExemptionCertificate certificate, TaxRule rule, DateTime onUtc)
    {
        if (certificate.Status != ExemptionStatus.Active)
        {
            return false;
        }

        if (certificate.EffectiveFromUtc.HasValue && onUtc < certificate.EffectiveFromUtc.Value)
        {
            return false;
        }

        if (certificate.ExpirationUtc.HasValue && onUtc >= certificate.ExpirationUtc.Value)
        {
            return false;
        }

        if (certificate.TaxTypes.Count > 0 && !certificate.TaxTypes.Contains(rule.TaxType, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (certificate.JurisdictionIds.Count > 0 &&
            !string.IsNullOrEmpty(rule.JurisdictionId) &&
            !certificate.JurisdictionIds.Contains(rule.JurisdictionId, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (certificate.ClassificationCodes.Count > 0 &&
            !string.IsNullOrEmpty(rule.CategoryCode) &&
            !certificate.ClassificationCodes.Contains(rule.CategoryCode, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
