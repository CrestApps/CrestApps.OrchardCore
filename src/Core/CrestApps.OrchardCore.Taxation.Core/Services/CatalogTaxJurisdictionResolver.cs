using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxJurisdictionResolver"/> backed by the <see cref="INamedCatalog{TaxJurisdiction}"/>. A
/// jurisdiction matches when every non-empty component it defines is satisfied by the address.
/// </summary>
public sealed class CatalogTaxJurisdictionResolver : ITaxJurisdictionResolver
{
    private readonly INamedCatalog<TaxJurisdiction> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTaxJurisdictionResolver"/> class.
    /// </summary>
    /// <param name="store">The jurisdiction store.</param>
    public CatalogTaxJurisdictionResolver(INamedCatalog<TaxJurisdiction> store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TaxJurisdiction>> ResolveAsync(Address address, DateTime onUtc, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            return [];
        }

        var jurisdictions = await _store.GetAllAsync(cancellationToken);

        return jurisdictions
            .Where(jurisdiction => IsEffective(jurisdiction, onUtc) && Matches(jurisdiction, address))
            .ToArray();
    }

    private static bool IsEffective(TaxJurisdiction jurisdiction, DateTime onUtc)
    {
        if (jurisdiction.EffectiveFromUtc.HasValue && onUtc < jurisdiction.EffectiveFromUtc.Value)
        {
            return false;
        }

        if (jurisdiction.EffectiveToUtc.HasValue && onUtc >= jurisdiction.EffectiveToUtc.Value)
        {
            return false;
        }

        return true;
    }

    private static bool Matches(TaxJurisdiction jurisdiction, Address address)
    {
        return ComponentMatches(jurisdiction.Country, address.Country) &&
            ComponentMatches(jurisdiction.Region, address.Region) &&
            ComponentMatches(jurisdiction.County, address.County) &&
            ComponentMatches(jurisdiction.City, address.City) &&
            ComponentMatches(jurisdiction.PostalCode, address.PostalCode);
    }

    private static bool ComponentMatches(string jurisdictionValue, string addressValue)
    {
        if (string.IsNullOrEmpty(jurisdictionValue))
        {
            return true;
        }

        return string.Equals(jurisdictionValue, addressValue, StringComparison.OrdinalIgnoreCase);
    }
}
