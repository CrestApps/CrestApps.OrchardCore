using System.Threading;
using System.Threading.Tasks;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Determines whether the merchant has an obligation (nexus) to collect a tax in a jurisdiction.
/// A jurisdiction levying a tax is not enough; the merchant must also be registered.
/// </summary>
public interface IMerchantTaxRegistrationProvider
{
    /// <summary>
    /// Determines whether the merchant is registered to collect the tax type in the jurisdiction.
    /// </summary>
    /// <param name="jurisdictionId">The identifier of the jurisdiction.</param>
    /// <param name="taxType">The tax type to check.</param>
    /// <param name="onUtc">The UTC date used to filter active registrations.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a nexus exists; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> HasNexusAsync(string jurisdictionId, string taxType, System.DateTime onUtc, CancellationToken cancellationToken = default);
}
