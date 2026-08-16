using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves the tax jurisdictions that apply to an address.
/// </summary>
public interface ITaxJurisdictionResolver
{
    /// <summary>
    /// Resolves the jurisdictions that apply to the supplied address on the supplied date.
    /// </summary>
    /// <param name="address">The address to resolve jurisdictions for.</param>
    /// <param name="onUtc">The UTC date used to filter effective jurisdictions.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The applicable jurisdictions.</returns>
    ValueTask<IReadOnlyList<TaxJurisdiction>> ResolveAsync(Address address, System.DateTime onUtc, CancellationToken cancellationToken = default);
}
