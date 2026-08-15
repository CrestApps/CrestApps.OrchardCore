using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Determines whether a customer is exempt from a tax for a given jurisdiction and classification.
/// </summary>
public interface ITaxExemptionResolver
{
    /// <summary>
    /// Determines whether the customer is exempt from the tax described by the supplied rule.
    /// </summary>
    /// <param name="customer">The customer tax profile, when a customer is known.</param>
    /// <param name="rule">The rule that would otherwise apply.</param>
    /// <param name="onUtc">The UTC date used to filter effective certificates.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the customer is exempt; otherwise <see langword="false"/>.</returns>
    ValueTask<bool> IsExemptAsync(CustomerTaxProfile customer, TaxRule rule, System.DateTime onUtc, CancellationToken cancellationToken = default);
}
