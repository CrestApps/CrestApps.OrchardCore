using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="IMerchantTaxRegistrationProvider"/> backed by the
/// <see cref="IMerchantTaxRegistrationStore"/>. When no registrations are configured the merchant is
/// treated as having nexus everywhere, which supports manual-rule scenarios out of the box. Once any
/// registration exists, nexus is enforced.
/// </summary>
public sealed class CatalogMerchantTaxRegistrationProvider : IMerchantTaxRegistrationProvider
{
    private readonly IMerchantTaxRegistrationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogMerchantTaxRegistrationProvider"/> class.
    /// </summary>
    /// <param name="store">The merchant tax registration store.</param>
    public CatalogMerchantTaxRegistrationProvider(IMerchantTaxRegistrationStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasNexusAsync(string jurisdictionId, string taxType, DateTime onUtc, CancellationToken cancellationToken = default)
    {
        var registrations = await _store.GetAllAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            return true;
        }

        return registrations.Any(registration => Covers(registration, jurisdictionId, taxType, onUtc));
    }

    private static bool Covers(MerchantTaxRegistration registration, string jurisdictionId, string taxType, DateTime onUtc)
    {
        if (!registration.IsActive)
        {
            return false;
        }

        if (!string.Equals(registration.JurisdictionId, jurisdictionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(registration.TaxType) &&
            !string.Equals(registration.TaxType, taxType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (registration.EffectiveFromUtc.HasValue && onUtc < registration.EffectiveFromUtc.Value)
        {
            return false;
        }

        if (registration.EffectiveToUtc.HasValue && onUtc >= registration.EffectiveToUtc.Value)
        {
            return false;
        }

        return true;
    }
}
